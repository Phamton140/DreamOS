using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DreamOS.Infrastructure.Data;
using LiteDB;
using Microsoft.IdentityModel.Tokens;

namespace DreamOS.Infrastructure.Services
{
    public class SecurityService
    {
        private readonly LiteDbContext _dbContext;
        private readonly byte[] _jwtKey;

        public SecurityService(LiteDbContext dbContext)
        {
            _dbContext = dbContext;
            _jwtKey = GetOrCreateJwtKey();
        }

        private byte[] GetOrCreateJwtKey()
        {
            var col = _dbContext.Database.GetCollection<BsonDocument>("security");
            var doc = col.FindOne(Query.EQ("_id", "jwt_signing_key"));

            if (doc != null && doc.TryGetValue("key", out var base64Val))
            {
                return Convert.FromBase64String(base64Val.AsString);
            }

            // Generar una clave criptográfica aleatoria de 256 bits (32 bytes)
            var keyBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes);
            }

            var newDoc = new BsonDocument();
            newDoc["_id"] = "jwt_signing_key";
            newDoc["key"] = Convert.ToBase64String(keyBytes);
            col.Insert(newDoc);

            return keyBytes;
        }

        public string HashToken(string token)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        public string GenerateTemporaryPairingCode(out string rawToken)
        {
            // Generar un token aleatorio temporal de emparejamiento
            var randomBytes = new byte[24];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            rawToken = Convert.ToBase64String(randomBytes);

            // Guardar en base de datos con expiración de 10 minutos
            var col = _dbContext.Database.GetCollection<BsonDocument>("pairing_codes");
            
            var doc = new BsonDocument();
            doc["_id"] = HashToken(rawToken);
            doc["expires_at"] = DateTime.UtcNow.AddHours(24);
            
            col.Insert(doc);

            return rawToken;
        }

        public bool ValidatePairingCode(string rawToken)
        {
            var hash = HashToken(rawToken);
            var col = _dbContext.Database.GetCollection<BsonDocument>("pairing_codes");
            var doc = col.FindOne(Query.EQ("_id", hash));

            if (doc == null) return false;

            var expiresAt = doc["expires_at"].AsDateTime;
            if (expiresAt <= DateTime.UtcNow)
            {
                col.Delete(hash);
                return false;
            }

            // Consumir el token una vez emparejado el dispositivo
            col.Delete(hash);
            return true;
        }

        public string GenerateJwtToken(string deviceId, string deviceName)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, deviceId),
                    new Claim(ClaimTypes.Name, deviceName)
                }),
                Expires = DateTime.UtcNow.AddYears(1), // Expiración larga para dispositivos vinculados
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(_jwtKey),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public TokenValidationParameters GetValidationParameters()
        {
            return new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_jwtKey),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };
        }
    }
}
