# Plano de Trabalho para o Codex — Fork mpv.net

Este documento organiza o trabalho inicial de manutenção do fork do **mpv.net**.

O objetivo é trabalhar com calma, corrigindo uma coisa por vez, começando pelas tarefas mais simples e avançando para as mais difíceis.

Este fork tem como foco:

- preservar o projeto original;
- respeitar o trabalho de Frank Skare / stax76;
- melhorar a documentação;
- corrigir bugs simples primeiro;
- evitar grandes mudanças de arquitetura no início;
- preparar o projeto para manutenção contínua.

## Documentos relacionados

- [README do fork](../README.md)
- [Modo portátil](PORTATIL.md)
- [Configuração](CONFIGURACAO.md)
- [Atalhos](ATALHOS.md)
- [Build e release](BUILD.md)
- [Roadmap](ROADMAP.md)
- [Template de bug](../.github/ISSUE_TEMPLATE/bug_report.md)
- [Template de melhoria](../.github/ISSUE_TEMPLATE/feature_request.md)

---

## Regra principal para o Codex

Antes de alterar qualquer arquivo:

1. Leia o README atual.
2. Leia este plano.
3. Liste quais arquivos pretende alterar.
4. Explique o motivo da alteração.
5. Faça uma alteração pequena por vez.
6. Ao final, informe:
   - o que foi alterado;
   - quais arquivos foram modificados;
   - como testar;
   - qual deve ser o próximo passo.

Não faça grandes refatorações sem necessidade.

---

# Etapa 1 — Organizar o README em português

## Objetivo

Transformar o README do fork em um documento claro para usuários e desenvolvedores brasileiros.

## Tarefas

- Explicar o que é o mpv.net.
- Informar que este repositório é um fork de manutenção.
- Dar crédito ao projeto original e ao mantenedor Frank Skare / stax76.
- Explicar que o foco inicial é estabilidade, documentação e correções simples.
- Criar seção de instalação.
- Criar seção de uso portátil.
- Criar seção de arquivos de configuração.
- Criar seção de como reportar bugs.
- Criar seção de roadmap.

## Resultado esperado

README em português, limpo, objetivo e fácil de entender.

---

# Etapa 2 — Documentar o modo portátil

## Problema

A versão ZIP é chamada de portátil, mas para funcionar como portátil real o usuário precisa criar uma pasta chamada:

```text
portable_config
```

ao lado do arquivo:

```text
mpvnet.exe
```

Sem essa pasta, as configurações podem ir para:

```text
C:\Users\<usuario>\AppData\Roaming\mpv.net
```

## Tarefas

- Criar o arquivo:

```text
docs/PORTATIL.md
```

- Explicar a estrutura correta:

```text
mpvnet.exe
portable_config/
  mpv.conf
  input.conf
  scripts/
  script-opts/
```

- Explicar que `portable_config` deve ficar na mesma pasta do `mpvnet.exe`.
- Explicar a diferença entre:
  - versão sem instalador;
  - versão realmente portátil.

## Melhoria futura

Verificar se o script de release pode incluir automaticamente a pasta `portable_config` no ZIP portátil.

---

# Etapa 3 — Criar exemplos de configuração

## Problema

Alguns usuários não encontram os arquivos:

```text
mpv.conf
input.conf
```

ou o programa cria arquivos vazios.

## Tarefas

Criar a pasta:

```text
docs/exemplos/
```

Criar os arquivos:

```text
docs/exemplos/mpv.conf
docs/exemplos/input.conf
docs/exemplos/thumbfast.conf
```

## Conteúdo esperado

O `mpv.conf` deve ter configurações simples e comentadas.

O `input.conf` deve ter atalhos básicos e explicar que teclas duplicadas podem causar conflito.

O `thumbfast.conf` deve explicar que, em alguns casos, pode ser necessário apontar para um `mpv.exe` separado.

---

# Etapa 4 — Documentar atalhos e conflitos no input.conf

## Problema

Alguns usuários alteram atalhos no `input.conf`, mas as alterações não funcionam.

Possíveis causas:

- mesma tecla configurada mais de uma vez;
- editor de atalhos com limitações;
- diferença entre configuração antiga e nova;
- conflito entre atalhos do mpv e do mpv.net.

## Tarefas

Criar o arquivo:

```text
docs/ATALHOS.md
```

Explicar:

- como editar `input.conf`;
- como evitar teclas duplicadas;
- que, se a mesma tecla aparecer mais de uma vez, a última configuração pode prevalecer;
- como restaurar atalhos padrão;
- como testar alteração de atalho.

## Melhoria futura

Criar no código uma validação para detectar teclas duplicadas antes de salvar o `input.conf`.

---

# Etapa 5 — Criar modelo para reportar bugs

## Problema

Muitas issues não trazem informações suficientes para reproduzir o erro.

## Tarefas

Criar:

```text
.github/ISSUE_TEMPLATE/bug_report.md
.github/ISSUE_TEMPLATE/feature_request.md
```

## O modelo de bug deve pedir:

- versão do mpv.net;
- versão do Windows;
- placa de vídeo;
- se está usando versão instalada ou portátil;
- se o problema acontece também no mpv.exe;
- passos para reproduzir;
- resultado esperado;
- resultado atual;
- arquivos `mpv.conf` e `input.conf`, se forem relevantes;
- logs, prints ou vídeos, se possível.

---

# Etapa 6 — Ajustar pacote portátil

## Problema

O pacote ZIP portátil deveria ser mais claro e fácil de usar.

## Tarefas

Verificar os scripts:

```text
src/Tools/release-mpv.net.ps1
src/Tools/update-mpv.ps1
```

Verificar se o pacote ZIP pode incluir:

```text
portable_config/
portable_config/mpv.conf
portable_config/input.conf
portable_config/scripts/
portable_config/script-opts/
```

## Resultado esperado

Ao baixar a versão portátil, o usuário já terá uma estrutura clara para configuração.

---

# Etapa 7 — Investigar problema de caminhos longos

## Issue relacionada

Problema: mpv.net não abre vídeos com caminho muito longo, enquanto o mpv.exe consegue abrir.

## Tarefas

- Criar teste com arquivo em caminho longo.
- Testar abertura via:
  - Explorer;
  - linha de comando;
  - outro programa externo.
- Verificar se o problema está no tratamento de argumentos.
- Verificar suporte a long path no Windows.
- Verificar uso de prefixo:

```text
\\?\
```

## Solução temporária

Documentar workaround usando script `.bat`, se necessário.

---

# Etapa 8 — Investigar thumbfast na versão portátil

## Problema

Na versão 7.1.2.0 portátil, o script thumbfast pode falhar.

Um usuário relatou que funcionou ao colocar um `mpv.exe` original na pasta raiz e configurar o `thumbfast.conf` para apontar para ele.

## Tarefas

- Testar thumbfast em versão portátil.
- Verificar se thumbfast chama `mpv.exe` ou `mpvnet.exe`.
- Verificar se o empacotamento self-contained afeta o script.
- Documentar configuração recomendada.
- Avaliar se o pacote deve incluir ou orientar o uso de `mpv.exe` separado.

---

# Etapa 9 — Investigar autoplay com pause=yes

## Problema

Mesmo usando:

```text
pause=yes
drag-and-drop=append
```

E fila de arquivos, alguns vídeos começam a tocar automaticamente.

## Tarefas

- Reproduzir o problema com vários vídeos.
- Testar abertura por:
  - menu do app;
  - arrastar e soltar;
  - associação de arquivo;
  - linha de comando.
- Verificar se `pause=yes` é perdido ao carregar próximo item.
- Verificar comportamento com playlist/fila.
- Criar correção pequena e testável.

---

# Etapa 10 — Bugs visuais e integração com Windows

## Problemas

- Flash branco ao fechar janela.
- Área fora do vídeo aparece cinza em vez de preta.
- Comportamentos diferentes após updates do Windows 11.

## Tarefas

- Verificar cor de fundo da janela.
- Verificar renderização ao fechar.
- Testar modo escuro.
- Comparar com mpv.exe.
- Criar correções pequenas, sem alterar arquitetura geral.

---

# Ordem recomendada de trabalho

1. README em português.
2. Documentação do modo portátil.
3. Exemplos de `mpv.conf` e `input.conf`.
4. Documentação de atalhos.
5. Templates de issue.
6. Ajuste do ZIP portátil.
7. Caminhos longos.
8. Thumbfast.
9. Autoplay.
10. Bugs visuais.
11. OneDrive/cloud files.
12. Melhorias grandes como uosc, yt-dlp, LUT e AI upscaling.

---

# Prompt padrão para iniciar cada tarefa no Codex

Use este modelo antes de cada correção:

```text
Leia o arquivo docs/PLANO-CODEX.md.

Vamos trabalhar apenas na etapa: [NOME DA ETAPA].

Antes de alterar qualquer arquivo:
1. Analise o problema.
2. Liste os arquivos envolvidos.
3. Explique a solução proposta.
4. Só depois faça alterações pequenas e objetivas.

Não faça refatoração grande.
Não altere comportamento não relacionado.
Ao final, informe como testar.
```

---

# Observação final

Este fork deve evoluir com calma.

A prioridade inicial não é criar muitas funcionalidades novas.

A prioridade é:

1. entender o projeto;
2. documentar bem;
3. corrigir bugs simples;
4. preparar o ambiente;
5. depois atacar problemas mais complexos.
