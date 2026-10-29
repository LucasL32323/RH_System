import 'package:flutter/material.dart';

import '../../core/theme/app_colors.dart';
import 'logo_hr_auto.dart';

/// Painel da marca, à esquerda da tela no desktop (>= 960 px).
/// Gradiente petróleo com formas decorativas, proposta de valor e um "cartão de ponto"
/// ilustrativo que antecipa a função principal do app.
class PainelMarca extends StatelessWidget {
  const PainelMarca({super.key});

  @override
  Widget build(BuildContext context) {
    final texto = Theme.of(context).textTheme;

    return Container(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [AppColors.primaria, AppColors.secundaria],
        ),
      ),
      child: LayoutBuilder(
        builder: (context, limites) {
          // Em janelas baixas (notebook com barra do navegador), esconde partes para não estourar a altura.
          final mostrarCartao = limites.maxHeight >= 720;
          final mostrarBeneficios = limites.maxHeight >= 600;

          return Stack(
            fit: StackFit.expand,
            children: [
              // Formas decorativas (círculos em #78CDD7 translúcido).
              const Positioned(top: -120, right: -80, child: _Circulo(diametro: 340, cor: AppColors.apoioTransl18)),
              const Positioned(bottom: -160, left: -100, child: _Circulo(diametro: 420, cor: AppColors.apoioTransl10)),
              const Positioned(bottom: 140, right: 60, child: _Circulo(diametro: 90, cor: AppColors.apoioTransl18)),
              Padding(
                padding: const EdgeInsets.fromLTRB(56, 48, 56, 40),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const LogoHrAuto(claro: true),
                    const Spacer(),
                    ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 460),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            'Seu RH no\npiloto automático.',
                            style: texto.displaySmall?.copyWith(
                              color: Colors.white,
                              fontWeight: FontWeight.w800,
                              height: 1.1,
                              letterSpacing: -1,
                            ),
                          ),
                          const SizedBox(height: 16),
                          Text(
                            'Ponto, banco de horas e compliance num só lugar, '
                            'com a visão certa para cada perfil.',
                            style: texto.titleMedium?.copyWith(color: AppColors.brancoTransl80, height: 1.45),
                          ),
                          if (mostrarBeneficios) ...const [
                            SizedBox(height: 32),
                            _Beneficio(
                              icone: Icons.fingerprint_rounded,
                              texto: 'Ponto com horário do servidor, mesmo offline',
                            ),
                            _Beneficio(
                              icone: Icons.insights_rounded,
                              texto: 'Dashboards para Colaborador, RH e Admin',
                            ),
                            _Beneficio(
                              icone: Icons.verified_user_outlined,
                              texto: 'Acesso protegido e dados tratados conforme a LGPD',
                            ),
                          ],
                          if (mostrarCartao) ...const [
                            SizedBox(height: 32),
                            _CartaoPonto(),
                          ],
                        ],
                      ),
                    ),
                    const Spacer(),
                    Text(
                      '© 2026 HR-Auto · TCC Desenvolvimento de Sistemas · SENAI Santos',
                      style: texto.bodySmall?.copyWith(color: AppColors.brancoTransl80),
                    ),
                  ],
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _Circulo extends StatelessWidget {
  const _Circulo({required this.diametro, required this.cor});

  final double diametro;
  final Color cor;

  @override
  Widget build(BuildContext context) => Container(
        width: diametro,
        height: diametro,
        decoration: BoxDecoration(shape: BoxShape.circle, color: cor),
      );
}

class _Beneficio extends StatelessWidget {
  const _Beneficio({required this.icone, required this.texto});

  final IconData icone;
  final String texto;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Row(
        children: [
          Container(
            width: 34,
            height: 34,
            decoration: BoxDecoration(
              color: AppColors.brancoTransl12,
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(icone, color: AppColors.apoio, size: 19),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              texto,
              style: Theme.of(context).textTheme.bodyLarge?.copyWith(color: Colors.white),
            ),
          ),
        ],
      ),
    );
  }
}

/// Ilustração: um registro de ponto confirmado, no estilo "vidro".
class _CartaoPonto extends StatelessWidget {
  const _CartaoPonto();

  @override
  Widget build(BuildContext context) {
    final texto = Theme.of(context).textTheme;
    return ExcludeSemantics(
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: AppColors.brancoTransl12,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.brancoTransl20),
        ),
        child: Row(
          children: [
            Container(
              width: 40,
              height: 40,
              decoration: const BoxDecoration(color: AppColors.apoio, shape: BoxShape.circle),
              child: const Icon(Icons.check_rounded, color: AppColors.primaria),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Entrada registrada',
                    style: texto.titleSmall?.copyWith(color: Colors.white, fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    'Hoje, 08:02 · FUNC-0007',
                    style: texto.bodySmall?.copyWith(color: AppColors.brancoTransl80),
                  ),
                ],
              ),
            ),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
              decoration: BoxDecoration(
                color: AppColors.brancoTransl20,
                borderRadius: BorderRadius.circular(20),
              ),
              child: Text(
                '+0h12 banco',
                style: texto.labelSmall?.copyWith(color: Colors.white, fontWeight: FontWeight.w600),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
