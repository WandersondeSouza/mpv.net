# Corrigir integração do yt-dlp e diagnóstico de verificação antibot

## Objetivo e autorização

Implementar as correções confirmadas pela auditoria de 18/10/2026 no MPV.NET,
preservando compatibilidade com mpv/libmpv e configurações explícitas do usuário.
Este prompt foi solicitado pelo usuário junto com sua execução no repositório.

## Evidências e entendimento atual

- O player tenta configurar `ytdl-path` como opção/propriedade nativa do mpv.
  A versão instalada responde `option not found` / `property not found`.
- O contrato oficial é `ytdl_hook-ytdl_path` em `script-opts`, ou `ytdl_path`
  em `script-opts/ytdl_hook.conf`. O bootstrap já adiciona os componentes ao PATH.
- A máquina possui yt-dlp 2026.08.19, Deno 2.9.7 e FFmpeg/FFprobe/FFplay
  N-127388-g615b07170e (12/10/2026), com hashes válidos. O executável oficial
  do yt-dlp inclui EJS. Não há evidência de dependência FFmpeg ausente.
- A enumeração plana do mix fornecido funcionou; a extração dos formatos do
  vídeo `tQ2UjfHNjOA` retornou `Sign in to confirm you're not a bot`.
  Listar metadados não comprova reprodução. A verificação do YouTube depende
  do serviço, da rede/IP e da sessão; uma correção local não garante sua remoção.
- A classificação atual transforma `sign in to confirm` em autenticação,
  confundindo verificação antibot com conteúdo privado ou restrito por idade.

## Arquivos e contratos a revisar

Leia AGENTS.md, README.md, .ai/skills/mpvnet-maintainer.md, docs/manual.md,
docs/guia-operacional.md e documentação relevante em docs/developer/.
Revise Player.Initialization.cs, RuntimeComponents.cs, CommandLine.cs,
StreamingFailureDiagnostics.cs, testes de integração nativa/diagnósticos e
catálogos gettext lang/source.pot e lang/po/*.po.

## Implementação solicitada

1. Substituir as chamadas inválidas a `ytdl-path` pelo contrato do hook.
   Configurar o yt-dlp resolvido pela política existente depois de o mpv ter
   carregado a configuração, antes de iniciar a reprodução; atualizar após o
   bootstrap sem recarregar mídia ou reconstruir a playlist.
2. Preservar todas as demais script-opts e caminhos definidos explicitamente
   em mpv.conf, CLI, perfis e script-opts/ytdl_hook.conf. Preservar também
   alterações feitas em runtime; acompanhar apenas o valor automático do player.
   Tratar com segurança espaços, vírgulas, caracteres Unicode e '=' em caminhos.
   Evitar copiar parsers de configuração do mpv ou reescrever arquivos do usuário.
3. Distinguir verificação antibot de autenticação com base em mensagens
   explícitas como `Sign in to confirm you're not a bot`, incluindo apóstrofos
   tipográficos e os caracteres U+0092/U+FFFD que podem resultar da saída
   CP1252 convertida como UTF-8 no Windows. Manter autenticação para conteúdo privado/restrito por idade,
   e categorias próprias para EJS, PO Token, HTTP e transporte.
   Não agendar reconexão automática para o desafio antibot.
4. Exibir mensagem curta gettext explicando a verificação exigida pelo serviço.
   Atualizar o template inglês e todos os 13 catálogos traduzidos, conservando
   paridade e redigindo os detalhes técnicos somente nos logs sanitizados.
5. Corrigir a documentação existente que descreve `ytdl-path` como opção nativa.
   Explicar configuração suportada, papel de FFmpeg/FFprobe, EJS/Deno e limites
   dos testes de playlist, cookies, tokens e bloqueios do YouTube.

## Limites e riscos

- Preservar a alteração preexistente de Package.appxmanifest e outros trabalhos.
- Não alterar a origem/variante do FFmpeg, fonte stable do yt-dlp, cache, hashes,
  libmpv, seleção de qualidade ou política de playlists sem falha comprovada.
- Não acessar cookies do navegador, criar tokens, habilitar providers/remote
  components silenciosamente nem prometer contornar verificação do YouTube.
- Não publicar, enviar commits ou substituir a instalação Store nesta tarefa.
- A preservação de configuração explícita é o maior risco da correção; cobri-la
  com testes que exercitem o mpv real e valores com caracteres especiais.

## Validação e critérios de conclusão

- Verificar a cobertura existente em src/MpvNet.Tests/Program.cs e acrescentar
  regressões significativas nos testes apropriados.
- Testar o hook nativo: aplicação do caminho automático, preservação de outras
  script-opts, caminho explícito, atualização automática após bootstrap,
  alteração em runtime e configuração do script. Confirmar ausência de chamadas
  inválidas e atualização sem alterar a playlist ou o item atual.
- Testar classificação antibot versus idade/login, EJS e PO Token; confirmar
  prioridade adequada, sanitização, mensagem localizada e ausência de retry.
- Executar build Windows, testes direcionados e suíte existente de forma serial
  usando MPVNET_LOCAL_APPDATA isolado. Validar paridade gettext e git diff --check.
- Usar exatamente https://www.youtube.com/watch?v=tQ2UjfHNjOA&list=RDalprRqKT5Lo&index=4
  para a validação online autorizada, limitando requisições. As URLs adicionais
  indicadas pelo usuário para validação são
  https://www.youtube.com/watch?v=XgCg0EuA18k&list=PL2aam9s5cyMSyzaEJfAgO0wkacgnXx5x6
  e https://www.youtube.com/watch?v=8zW4pa7zSQY&list=PL2aam9s5cyMR9BEcq2lISib3sLcUNfF58.
  Separar enumeração
  de playlist, obtenção de formatos, carregamento no libmpv e imagem/áudio reais.
- Usar checklist manual de libmpv do AGENTS.md; registrar itens não observados
  como pendentes, sem apresentá-los como aprovados.
- Entregar arquivos alterados, evidências dos testes e limitações externas.
  O sucesso local é corrigir os defeitos do frontend; acesso ao vídeo depende
  também da resposta atual do YouTube.
