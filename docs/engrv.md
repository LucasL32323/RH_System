# Engenharia Reversa (engrv) — anotações de estudo do Lucas

## Fundamentos
- Compilação: código C → compilador → Assembly → montador → código de máquina. A RE faz o caminho inverso (disassembly e decompilação).
- Estática: analisa sem executar (objdump, Ghidra). Dinâmica: executa e observa (GDB, x64dbg).
- Convenção System V (Linux): argumentos em rdi, rsi, rdx, rcx, r8, r9; retorno em eax/rax. Windows x64: rcx, rdx, r8, r9.
- Prólogo `push rbp; mov rbp,rsp` e epílogo `pop rbp; ret` marcam início/fim de função. `[rbp-0x4]` = variável local.
- Chamadas `@plt` = funções externas (libc). Rastreá-las é o primeiro passo pra entender o comportamento de um binário.
- "not stripped" = nomes de funções preservados; malware costuma vir stripped.

## Ferramentas
| Ferramenta | Preço | Função |
|---|---|---|
| objdump | grátis | disassembler de terminal |
| Ghidra | grátis | disassembler + decompilador |
| radare2 | grátis | análise + debug |
| IDA Pro / IDA Free | pago / grátis | disassembler |
| GDB / x64dbg | grátis | debugger (Linux / Windows) |

## Ambiente escolhido: WSL2 (não precisa de SSD novo, ~2–3 GB)
1. PowerShell (admin): `wsl --install -d Ubuntu`, reiniciar.
2. Ubuntu: `sudo apt update && sudo apt install -y gcc gdb binutils file`
3. VS Code: extensões WSL e C/C++; `mkdir ~/re && cd ~/re && code .`
4. Alvo `soma.c`:
```c
#include <stdio.h>
int soma(int a, int b) { return a + b; }
int main() { printf("%d\n", soma(2, 3)); return 0; }
```
   `gcc -O0 -g soma.c -o soma && ./soma`
5. Estática: `file soma` e `objdump -d -M intel soma | grep -A12 "<soma>:"`
6. Dinâmica: `gdb ./soma` → `break soma`, `run`, `info registers rdi rsi`, `finish`, `info registers rax`
7. Próximo nível: Ghidra (JDK 21) abrindo o mesmo binário.

## Desafios
1. Compilar com `-O2` e ver o que acontece com `soma`.
2. Compilar com `-s` (stripped) e achar a função sem nome.
3. Trocar por `if`/`for` e ver como vira Assembly.

## Segurança
- WSL2 é uma VM leve integrada ao Windows: enxerga /mnt/c e usa a rede do PC. Serve para estudo (binários próprios, crackmes, CTFs), NÃO para rodar malware.
- Malware real: só em VM (VirtualBox) com rede desligada e snapshot.
- Limites: entender e defender sim; escrever malware/exploits ou quebrar licença/DRM/anti-cheat não.

## Onde treinar
crackmes.one, TryHackMe, HackTheBox, livro Practical Malware Analysis.

## Status
Próximo passo: fazer os passos 1–6 e analisar juntos a saída do objdump.
