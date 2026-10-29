import 'package:flutter/material.dart';

import 'core/theme/app_theme.dart';
import 'presentation/pages/fundacao_page.dart';

/// Widget raiz do aplicativo.
class HrAutoApp extends StatelessWidget {
  const HrAutoApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'HR-Auto',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.claro(),
      home: const FundacaoPage(),
    );
  }
}
