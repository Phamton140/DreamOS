using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DreamOS.Core.Interfaces;
using DreamOS.Infrastructure.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using LiteDB;

namespace DreamOS.Infrastructure.Services
{
    public class GoogleDriveStorageProvider : IStorageProvider
    {
        private readonly LiteDbContext _dbContext;
        private readonly ICloudflareTunnelService _tunnelService;
        private DriveService? _driveService;
        private UserCredential? _credential;

        public string ProviderName => "GoogleDrive";

        public GoogleDriveStorageProvider(LiteDbContext dbContext, ICloudflareTunnelService tunnelService)
        {
            _dbContext = dbContext;
            _tunnelService = tunnelService;
        }

        private string GetClientId()
        {
            var col = _dbContext.Database.GetCollection<BsonDocument>("settings");
            var doc = col.FindOne(Query.EQ("_id", "google_client_id"));
            return doc != null && doc.TryGetValue("value", out var val) ? val.AsString : "YOUR_DEFAULT_CLIENT_ID.apps.googleusercontent.com";
        }

        private string GetClientSecret()
        {
            var col = _dbContext.Database.GetCollection<BsonDocument>("settings");
            var doc = col.FindOne(Query.EQ("_id", "google_client_secret"));
            return doc != null && doc.TryGetValue("value", out var val) ? val.AsString : "YOUR_DEFAULT_CLIENT_SECRET";
        }

        public async Task<bool> AuthenticateAsync(Action<string> authUrlCallback)
        {
            var clientId = GetClientId();
            var clientSecret = GetClientSecret();

            if (clientId.Contains("YOUR_DEFAULT") || clientSecret.Contains("YOUR_DEFAULT"))
            {
                throw new InvalidOperationException("Credenciales de Google API (Client ID / Client Secret) no configuradas en el agente.");
            }

            // Intentar cargar tokens almacenados
            var col = _dbContext.Database.GetCollection<BsonDocument>("tokens");
            var doc = col.FindOne(Query.EQ("_id", "google_drive_tokens"));

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                },
                Scopes = new[] { DriveService.Scope.DriveFile }
            });

            if (doc != null && doc.TryGetValue("access_token", out var acc) && doc.TryGetValue("refresh_token", out var refToken))
            {
                var token = new TokenResponse
                {
                    AccessToken = acc.AsString,
                    RefreshToken = refToken.AsString,
                    IssuedUtc = doc.TryGetValue("issued_utc", out var iss) ? DateTime.Parse(iss.AsString) : DateTime.UtcNow,
                    ExpiresInSeconds = doc.TryGetValue("expires_in", out var exp) ? exp.AsInt64 : 3600
                };

                _credential = new UserCredential(flow, "user", token);
                
                // Verificar / refrescar token
                try
                {
                    if (_credential.Token.IsExpired(flow.Clock))
                    {
                        var refreshed = await _credential.RefreshTokenAsync(CancellationToken.None);
                        if (refreshed)
                        {
                            SaveTokenResponse(_credential.Token);
                        }
                    }

                    InitializeDriveService();
                    return true;
                }
                catch (Exception)
                {
                    // Token inválido o revocado, flujo de login nuevo
                }
            }

            // Si no hay token o falló, construir URL para que el usuario inicie sesión
            var redirectUri = _tunnelService.IsRunning 
                ? $"{_tunnelService.TunnelUrl.TrimEnd('/')}/api/storage/google/callback" 
                : "http://localhost:5000/api/storage/google/callback";

            var authorizationUrl = flow.CreateAuthorizationCodeRequest(redirectUri).Build().ToString();
            authUrlCallback(authorizationUrl);
            return false;
        }

        public async Task ProcessCallbackCodeAsync(string code)
        {
            var clientId = GetClientId();
            var clientSecret = GetClientSecret();
            var redirectUri = _tunnelService.IsRunning 
                ? $"{_tunnelService.TunnelUrl.TrimEnd('/')}/api/storage/google/callback" 
                : "http://localhost:5000/api/storage/google/callback";

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                },
                Scopes = new[] { DriveService.Scope.DriveFile }
            });

            var token = await flow.ExchangeCodeForTokenAsync("user", code, redirectUri, CancellationToken.None);
            _credential = new UserCredential(flow, "user", token);
            
            SaveTokenResponse(token);
            InitializeDriveService();
        }

        private void SaveTokenResponse(TokenResponse token)
        {
            var col = _dbContext.Database.GetCollection<BsonDocument>("tokens");
            var doc = new BsonDocument();
            doc["_id"] = "google_drive_tokens";
            doc["access_token"] = token.AccessToken;
            doc["refresh_token"] = token.RefreshToken ?? string.Empty;
            doc["issued_utc"] = token.IssuedUtc.ToString("O");
            doc["expires_in"] = token.ExpiresInSeconds ?? 3600;

            col.Upsert(doc);
        }

        private void InitializeDriveService()
        {
            if (_credential == null) throw new InvalidOperationException("Credenciales no inicializadas.");
            
            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = "DreamOS Dev Agent"
            });
        }

        public async Task<string> UploadFileAsync(string localPath, string remoteFolder, string fileName)
        {
            if (_driveService == null)
            {
                throw new InvalidOperationException("Google Drive no autenticado. Llama a AuthenticateAsync primero.");
            }

            // 1. Buscar o crear la carpeta remota
            var folderId = await GetOrCreateFolderAsync(remoteFolder);

            // 2. Preparar subida de archivo
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = fileName,
                Parents = new List<string> { folderId }
            };

            await using var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read);
            var request = _driveService.Files.Create(fileMetadata, stream, "application/vnd.android.package-archive");
            request.Fields = "id, webViewLink, webContentLink";

            var uploadProgress = await request.UploadAsync();
            if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                throw new Exception($"Fallo al subir a Google Drive: {uploadProgress.Exception?.Message}", uploadProgress.Exception);
            }

            var uploadedFile = request.ResponseBody;
            // Devolver link de descarga directa o enlace webView
            return uploadedFile.WebViewLink ?? uploadedFile.Id;
        }

        public async Task<List<string>> ListFilesAsync(string remoteFolder)
        {
            if (_driveService == null)
            {
                throw new InvalidOperationException("Google Drive no autenticado.");
            }

            var folderId = await GetFolderIdAsync(remoteFolder);
            if (string.IsNullOrEmpty(folderId))
            {
                return new List<string>();
            }

            var request = _driveService.Files.List();
            request.Q = $"'{folderId}' in parents and trashed = false";
            request.Fields = "files(name, webViewLink)";

            var result = await request.ExecuteAsync();
            var fileList = new List<string>();
            foreach (var file in result.Files)
            {
                fileList.Add($"{file.Name}: {file.WebViewLink}");
            }
            return fileList;
        }

        private async Task<string> GetOrCreateFolderAsync(string folderName)
        {
            var folderId = await GetFolderIdAsync(folderName);
            if (!string.IsNullOrEmpty(folderId))
            {
                return folderId;
            }

            // Crear carpeta
            var folderMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };

            var request = _driveService!.Files.Create(folderMetadata);
            request.Fields = "id";
            var folder = await request.ExecuteAsync();
            return folder.Id;
        }

        private async Task<string?> GetFolderIdAsync(string folderName)
        {
            var request = _driveService!.Files.List();
            request.Q = $"mimeType = 'application/vnd.google-apps.folder' and name = '{folderName}' and trashed = false";
            request.Fields = "files(id)";
            var result = await request.ExecuteAsync();
            if (result.Files.Count > 0)
            {
                return result.Files[0].Id;
            }
            return null;
        }
    }
}
