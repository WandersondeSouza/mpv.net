# Plano de Trabalho para o Codex — Fork mpv.net

Este documento organiza a fase atual de manutenção do fork do **mpv.net**.

O fork deixou a fase de documentação inicial pura e agora entra em uma fase de **manutenção prática assistida**, com foco em bugs pequenos, melhorias de empacotamento e documentação complementar conforme cada correção for feita.

## Objetivo atual

Trabalhar com calma, corrigindo uma coisa por vez, começando pelas tarefas mais simples e avançando para as mais difíceis.

A documentação básica já foi iniciada. A partir de agora, a prioridade é:

- corrigir bugs pequenos e bem delimitados;
- validar o pacote portátil;
- melhorar a experiência inicial do usuário;
- manter a documentação atualizada junto com cada correção;
- evitar grandes mudanças de arquitetura;
- preparar o repositório para manutenção contínua e segura.

## Estratégia da fase atual

A divisão recomendada de esforço nesta fase é:

```text
70% correção de bugs e melhorias pequenas
30% documentação complementar e organização
```

Isto significa que não devemos continuar apenas documentando. Também não devemos iniciar grandes funcionalidades novas.

O foco correto agora é transformar a documentação existente em trabalho prático, sempre com alterações pequenas, rastreáveis e fáceis de testar.

## Foco do fork

Este fork tem como foco:

- preservar o projeto original;
- respeitar o trabalho de Frank Skare / stax76;
- manter compatibilidade com mpv/libmpv sempre que possível;
- melhorar a documentação em português brasileiro;
- corrigir bugs simples primeiro;
- melhorar o pacote portátil e a experiência de configuração;
- evitar grandes mudanças de arquitetura no início;
- preparar o projeto para manutenção contínua.

## O que pode ser feito agora

Nesta fase, o Codex pode trabalhar em:

- ajustes no pacote portátil;
- validação da pasta `portable_config`;
- melhorias simples em arquivos de configuração;
- investigação de atalhos duplicados no `input.conf`;
- correções pequenas e testáveis de bugs;
- documentação complementar relacionada ao bug corrigido;
- melhoria de templates, instruções de build e instruções de teste.

## O que ainda deve ser evitado

Nesta fase, o Codex não deve fazer:

- grandes refatorações;
- mudança ampla de arquitetura;
- troca de tecnologia de interface;
- alteração grande no ciclo de vida do player;
- funcionalidades grandes como IPTV, AI upscaling, LUT, uosc ou mudanças profundas no yt-dlp;
- alterações que mexam em muitos arquivos sem necessidade clara;
- correções sem explicar como testar.

## Documentos relacionados

- [README do fork](../README.md)
- [Modo portátil](PORTATIL.md)
- [Configuração](CONFIGURACAO.md)
- [Atalhos](ATALHOS.md)
- [Build e release](BUILD.md)
- [Roadmap](ROADMAP.md)
- [Prompt de proximas melhorias](../.ai/prompts/next-improvements.md)
- [Template de bug](../.github/ISSUE_TEMPLATE/bug_report.md)
- [Template de melhoria](../.github/ISSUE_TEMPLATE/feature_request.md)

---

## Regra principal para o Codex

Antes de alterar qualquer arquivo:

1. Leia o README atual.
2. Leia este plano.
3. Identifique exatamente qual etapa será trabalhada.
4. Liste quais arquivos pretende alterar.
5. Explique o motivo da alteração.
6. Faça uma alteração pequena por vez.
7. Ao final, informe:
   - o que foi alterado;
   - quais arquivos foram modificados;
   - como testar;
   - quais riscos existem;
   - qual deve ser o próximo passo.

Não faça grandes refatorações sem necessidade.

---

# Situação atual da documentação inicial

A documentação inicial já está suficiente para iniciar bugs e melhorias pequenas.

Já existem documentos e estruturas para:

- README do fork;
- modo portátil;
- configuração;
- atalhos;
- build e release;
- roadmap;
- exemplos de configuração;
- templates de bug e melhoria.

Portanto, a documentação continua importante, mas agora deve acompanhar as correções reais.

Regra prática:

```text
Corrigiu um bug ou melhorou um comportamento?
Atualize também a documentação relacionada, se necessário.
```

---

# Ordem recomendada da fase atual

## Etapa 1 — Validar pacote portátil e portable_config

Status: validado em dry run local em 2026-05-21 com `-SkipGitHubRelease -SkipInstaller`; pendente validar instalador, publicação GitHub e revisão manual completa da UI.

### Objetivo

Confirmar se o pacote ZIP portátil entrega uma estrutura clara para o usuário e se a pasta `portable_config` pode ser incluída automaticamente ou orientada de forma mais objetiva.

### Arquivos prováveis

```text
src/Tools/release-mpv.net.ps1
src/Tools/update-mpv.ps1
docs/PORTATIL.md
docs/BUILD.md
docs/exemplos/mpv.conf
docs/exemplos/input.conf
docs/exemplos/portable_config/mpv.conf
docs/exemplos/portable_config/input.conf
```

### Tarefas

- Verificar como o ZIP portátil é montado. Concluído.
- Verificar se existe lógica para incluir arquivos/pastas extras. Concluído.
- Incluir no pacote:

```text
portable_config/
portable_config/mpv.conf
portable_config/input.conf
portable_config/scripts/
portable_config/script-opts/
```

- Atualizar a documentação do modo portátil.
- Validar o ZIP gerado em uma execução local do script de release. Concluído em 2026-05-21: o ZIP `mpv.net-v7.1.2.0-portable-x64.zip` foi gerado, inspecionado e contém `mpvnet.exe`, dependências nativas, ferramentas auxiliares, `Locale/` e `portable_config/`.
- Smoke test do executável extraído com vídeo MP4 e imagem PNG gerados localmente. Concluído em 2026-05-21.
- Validar instalador, publicação GitHub e testes manuais de fullscreen, menu, atalhos, persistência de configuração e temas.

### Resultado esperado

O usuário deve entender claramente a diferença entre versão sem instalador e versão realmente portátil.

---

## Etapa 2 — Validar atalhos e conflitos no input.conf

Status: fluxo de código validado por inspeção e build em 2026-05-21; pendente apenas validação visual interativa da janela.

### Objetivo

Investigar se há duplicidade ou conflito de atalhos que impeça alterações feitas pelo usuário de funcionarem corretamente.

### Arquivos prováveis

```text
docs/ATALHOS.md
docs/exemplos/input.conf
```

Também procurar no código por áreas relacionadas ao editor de atalhos e salvamento de `input.conf`.

### Tarefas

- Localizar onde o `input.conf` é carregado e salvo. Concluído.
- Verificar se o app pode gerar atalhos duplicados. Concluído.
- Verificar se há diferença entre atalhos do mpv e atalhos do mpv.net. Concluído.
- Implementar validação simples no editor para duplicidade de tecla com comandos diferentes. Concluído.
- Atualizar a documentação com a regra prática para evitar duplicidade. Concluído.
- Confirmar no código que `InputWindow` cancela o fechamento e mostra aviso quando a mesma tecla possui comandos diferentes. Concluído por inspeção e build.

### Resultado esperado

Reduzir confusão do usuário ao editar atalhos.

---

## Etapa 3 — Investigar thumbfast na versão portátil

Status: validado em 2026-05-21 com `thumbfast.lua` real no pacote portátil; pendente apenas validação visual de thumbnails em uma UI de usuário final.

### Objetivo

Verificar se o script thumbfast falha na versão portátil por depender de `mpv.exe` separado ou por limitação do empacotamento.

### Arquivos prováveis

```text
docs/exemplos/thumbfast.conf
docs/PORTATIL.md
docs/CONFIGURACAO.md
```

Também procurar no repositório referências a `thumbfast`, `script-opts`, `mpv.exe` e scripts Lua.

### Tarefas

- Verificar como scripts são carregados no modo portátil. Concluído.
- Verificar se `thumbfast.conf` precisa apontar para um `mpv.exe` separado. Concluído: no mpv.net v7, não por padrão.
- Documentar configuração recomendada. Concluído.
- Evitar incluir binários externos sem análise. Concluído: o pacote não inclui `mpv.exe` separado.
- Validar carregamento real de `portable_config/scripts/thumbfast.lua` e `portable_config/script-opts/thumbfast.conf`. Concluído em 2026-05-21: o log registrou carregamento do script, leitura da configuração e mensagens `thumbfast-info` sem erros.

### Resultado esperado

Ter uma orientação clara para usuários que usam thumbfast na versão portátil.

---

## Etapa 4 — Investigar caminhos longos no Windows

Status: corrigido e validado por terminal e instância única em 2026-05-21; pendente validar Explorer, associação de arquivo e menu `Open Files...`.

### Objetivo

Entender por que o mpv.net pode falhar com caminhos muito longos enquanto o `mpv.exe` consegue abrir.

### Arquivos prováveis

Procurar no código por:

```text
args
command line
open file
playlist
File.Exists
Path
```

### Tarefas

- Criar cenário de teste com caminho longo. Concluído para validação .NET local.
- Verificar abertura via Explorer.
- Verificar abertura via linha de comando. Concluído em 2026-05-21 com arquivo MP4 em caminho de 325 caracteres.
- Verificar abertura por associação de arquivo.
- Avaliar uso de prefixo:

```text
\\?\
```

- Propor correção pequena apenas se o ponto exato for identificado. Concluído: `Player.ConvertFilePath` converte arquivos locais longos para `\\?\` antes de chamar `loadfile`.
- Validar repasse à instância única. Concluído em 2026-05-21: a segunda instância encerrou após enviar o arquivo e a primeira instância abriu o caminho longo normalizado.
- Caso contrário, documentar workaround temporário.

### Resultado esperado

Identificar se o problema está no tratamento de argumentos, normalização de caminho ou limitação do Windows/.NET.

---

## Etapa 5 — Investigar autoplay com pause=yes

### Objetivo

Investigar por que alguns vídeos podem começar automaticamente mesmo com:

```text
pause=yes
drag-and-drop=append
```

### Tarefas

- Reproduzir o problema com múltiplos vídeos.
- Testar abertura pelo menu do app.
- Testar arrastar e soltar.
- Testar associação de arquivo.
- Testar linha de comando.
- Verificar se `pause=yes` é perdido ao carregar próximo item.
- Criar correção pequena e testável, se possível.

### Resultado esperado

Entender se o problema vem do fluxo de playlist/fila ou de alguma opção sobrescrita pelo mpv.net.

---

## Etapa 6 — Investigar bugs visuais simples

### Objetivo

Tratar bugs visuais pequenos e bem delimitados.

### Exemplos

- Flash branco ao fechar janela.
- Área fora do vídeo cinza em vez de preta.
- Comportamentos visuais diferentes após atualizações do Windows 11.

### Tarefas

- Verificar cor de fundo da janela.
- Verificar renderização ao fechar.
- Testar modo escuro.
- Comparar com `mpv.exe` quando fizer sentido.
- Evitar mexer no ciclo de vida completo da janela sem necessidade.

### Resultado esperado

Correções pequenas de UX visual sem alterar arquitetura geral.

---

# Backlog futuro

Somente depois da fase atual, avaliar:

- OneDrive/cloud files;
- integração mais avançada com yt-dlp;
- uosc;
- LUT;
- AI upscaling;
- melhorias grandes de UX;
- recursos voltados a IPTV ou media center.

Esses itens não são prioridade agora.

---

# Prompt padrão para iniciar cada tarefa no Codex

Use este modelo antes de cada correção:

Para rodadas gerais de organizacao, priorizacao e melhoria documental, use tambem:

```text
.ai/prompts/next-improvements.md
```

```text
Leia o arquivo docs/PLANO-CODEX.md.

Vamos trabalhar apenas na etapa: [NOME DA ETAPA].

Objetivo desta rodada:
[DESCREVA O OBJETIVO EM UMA FRASE]

Antes de alterar qualquer arquivo:
1. Analise o problema.
2. Liste os arquivos envolvidos.
3. Explique a solução proposta.
4. Informe o risco da alteração.
5. Só depois faça alterações pequenas e objetivas.

Regras:
- Não faça refatoração grande.
- Não altere comportamento não relacionado.
- Não mexa em muitos arquivos sem necessidade.
- Não implemente funcionalidades grandes.
- Se a correção exigir mais investigação, pare e explique o que encontrou.

Ao final, informe:
- o que foi alterado;
- quais arquivos foram modificados;
- como testar;
- se a documentação foi atualizada;
- qual deve ser o próximo passo.
```

---

# Prompt recomendado para iniciar amanhã

Use este prompt para começar a primeira tarefa prática:

```text
Leia o arquivo docs/PLANO-CODEX.md.

Estamos iniciando a fase de manutenção prática do fork mpv.net.

Trabalhe apenas na Etapa 1: Validar pacote portátil e portable_config.

Objetivo desta rodada:
Verificar se o pacote ZIP portátil pode incluir automaticamente uma estrutura portable_config básica, com arquivos de exemplo de configuração, sem quebrar o comportamento atual do mpv.net.

Antes de alterar qualquer arquivo:
1. Leia o README.md.
2. Leia docs/PLANO-CODEX.md.
3. Leia docs/PORTATIL.md.
4. Leia docs/BUILD.md.
5. Analise os scripts src/Tools/release-mpv.net.ps1 e src/Tools/update-mpv.ps1.
6. Liste os arquivos que pretende alterar.
7. Explique qual alteração pretende fazer e por quê.
8. Informe os riscos antes de modificar.

Regras:
- Não faça refatoração grande.
- Não altere comportamento não relacionado.
- Não mexa em funcionalidades de player, libmpv, interface ou atalhos nesta rodada.
- Não inclua binários externos.
- Faça apenas uma alteração pequena e testável.
- Se não for seguro alterar o script de release agora, apenas documente a conclusão e proponha o próximo passo.

Resultado esperado:
- Confirmar como o ZIP portátil é gerado.
- Avaliar se é seguro incluir portable_config no pacote.
- Se for seguro, ajustar o script de release para incluir uma estrutura portable_config básica.
- Atualizar docs/PORTATIL.md ou docs/BUILD.md se necessário.

Ao final, informe:
- resumo da análise;
- arquivos alterados;
- como testar a geração do pacote portátil;
- riscos conhecidos;
- próximo passo recomendado.
```

---

# Observação final

O objetivo agora é evoluir com segurança.

A documentação não para, mas deixa de ser o trabalho principal isolado.

A regra da fase atual é:

```text
Corrigir pequeno, testar bem, documentar o necessário e avançar para o próximo bug.
```
