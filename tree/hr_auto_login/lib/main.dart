import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_fonts/google_fonts.dart';

import 'app.dart';

void main() {
  // A Inter vai junto do app (assets/google_fonts): abre sem internet e sem depender do CDN de fontes.
  GoogleFonts.config.allowRuntimeFetching = false;
  runApp(const ProviderScope(child: HrAutoApp()));
}
