#!/usr/bin/env bash
# Inicia o repositório Git do HR-Auto com um commit por parte da fundação.
# Uso: rode na raiz do projeto (a pasta que contém backend/ e frontend/).
set -euo pipefail

if [ ! -d backend ] || [ ! -d frontend ]; then
  echo "Execute na raiz do projeto (pasta com backend/ e frontend/)." >&2
  exit 1
fi

if [ -d .git ]; then
  echo "Já existe um repositório Git aqui. Nada a fazer."
  exit 0
fi

git init -b main

git add .gitignore README.md scripts
git commit -m "Adiciona README, .gitignore e scripts de apoio"

git add backend
git commit -m "Cria solução .NET com Clean Architecture e endpoint de status"

git add frontend
git commit -m "Cria base do app Flutter com tema, MVVM e tela de conexão"

echo
echo "Repositório criado. Para publicar no GitHub:"
echo "  git remote add origin <URL-do-repositorio>"
echo "  git push -u origin main"
