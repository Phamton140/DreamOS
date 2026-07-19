import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

class AppTheme {
  static const primaryColor = Color(0xFF6C5CE7); // Deep modern indigo
  static const accentColor = Color(0xFF00CEC9);  // Vibrant cyan
  static const backgroundColor = Color(0xFF0F0F1E); // Velvet deep dark background
  static const cardColor = Color(0xFF1D1D30);       // Dark gray/indigo card base
  static const secondaryTextColor = Color(0xFFA0A0C0);
  
  static ThemeData get darkTheme {
    return ThemeData(
      brightness: Brightness.dark,
      primaryColor: primaryColor,
      scaffoldBackgroundColor: backgroundColor,
      cardColor: cardColor,
      colorScheme: const ColorScheme.dark(
        primary: primaryColor,
        secondary: accentColor,
        surface: backgroundColor,
        error: Color(0xFFFF7675),
      ),
      textTheme: GoogleFonts.outfitTextTheme(ThemeData.dark().textTheme).copyWith(
        bodyLarge: GoogleFonts.outfit(color: Colors.white, fontSize: 16),
        bodyMedium: GoogleFonts.outfit(color: secondaryTextColor, fontSize: 14),
        titleLarge: GoogleFonts.outfit(color: Colors.white, fontSize: 20, fontWeight: FontWeight.bold),
      ),
      appBarTheme: AppBarTheme(
        backgroundColor: backgroundColor,
        elevation: 0,
        titleTextStyle: GoogleFonts.outfit(color: Colors.white, fontSize: 20, fontWeight: FontWeight.w600),
        iconTheme: const IconThemeData(color: Colors.white),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: primaryColor,
          foregroundColor: Colors.white,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
          padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 24),
          textStyle: GoogleFonts.outfit(fontSize: 16, fontWeight: FontWeight.bold),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: const Color(0xFF151528),
        hintStyle: GoogleFonts.outfit(color: const Color(0xFF606080)),
        labelStyle: GoogleFonts.outfit(color: secondaryTextColor),
        contentPadding: const EdgeInsets.symmetric(vertical: 16, horizontal: 20),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide.none,
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: accentColor, width: 1.5),
        ),
      ),
    );
  }
}
