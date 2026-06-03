# Changelog

Este arquivo registra apenas as versoes publicadas pelo fork
`WandersondeSouza/mpv.net`.

# Fork WandersondeSouza - v7.1.2.8 (2026-06-02)

## Identidade e distribuicao

- Reforcada a identidade do fork como `MPV.NET Media Player` em metadados,
  documentacao, materiais de IA e superficies do instalador.
- Ajustado o processo de geracao dos pacotes portatil e executavel para manter
  nomes de artefatos, instalador e fluxo de release alinhados ao fork.
- Mantida a compatibilidade tecnica com `mpvnet.exe`, configuracoes existentes e
  caminhos usados pelo mpv.net original.

## Playlists e titulos

- Ajustado o carregamento de playlists para preservar melhor titulos extraidos e
  itens adicionados ao player.
- Refinada a normalizacao do titulo da faixa exibido na interface, evitando que
  extensoes e nomes brutos prejudiquem a leitura da playlist.
- A playlist criada automaticamente pelo mpv ao abrir arquivos de uma pasta
  agora recebe titulos normalizados para o seletor de playlist, incluindo a
  remocao de aspas simples e duplas.
- Centralizado o ponto de escrita da playlist temporaria para aplicar a mesma
  normalizacao de titulos em playlists locais, playlists de pasta e listas ja
  criadas pelo mpv.

# Fork WandersondeSouza - v7.1.2.7 (2026-06-01)

## Manutencao operacional

- Ajustados scripts e cache de dependencias nativas do fluxo de release para
  tornar o empacotamento x64 mais previsivel.
- Alinhados os metadados do produto, do instalador e dos artefatos ZIP/EXE para
  usar `MPV.NET Media Player`.
- Atualizados materiais operacionais do fork para manter o caminho de publicacao
  alinhado ao workflow manual do GitHub Actions.

## Interface e localizacao

- Ajustado comportamento de menus internos e selecao de idioma da interface.
- Refinada a exibicao de titulos na camada de playlist.
- A normalizacao de titulos agora remove extensoes conhecidas, limpa caracteres
  especiais, limita a exibicao a 100 caracteres e usa um titulo padrao quando o
  resultado fica vazio.
- A playlist criada automaticamente ao abrir uma pasta agora exibe titulos
  normalizados desde o carregamento inicial e tambem inclui os itens extraidos
  de arquivos de playlist presentes na pasta.

# Fork WandersondeSouza - v7.1.2.6 (2026-05-31)

## Playlists locais

- Adicionado parser do frontend para playlists locais `.m3u`, `.m3u8`,
  `.pls`, `.xspf`, `.asx`, `.wpl`, `.cue` e `.jspf`, extraindo titulo e caminho
  de cada item antes de encaminhar a midia ao mpv/libmpv.
- URLs HTTP/HTTPS sem extensao reconhecida agora sao verificadas antes do
  `loadfile`; quando o conteudo remoto inicia com `#EXTM3U`, a playlist e
  baixada para um arquivo temporario e expandida pelo frontend.
- Ao abrir uma playlist, o primeiro item reproduzivel passa a iniciar a
  reproducao quando o player esta vazio; os demais itens sao anexados a
  playlist. Se uma instancia ja estiver aberta, os itens da playlist recebida
  sao apenas adicionados.
- Entradas repetidas que apontam para o mesmo arquivo ou URL sao ignoradas,
  mesmo quando possuem titulos diferentes.
- O seletor de playlist passa a mostrar os titulos extraidos de playlists
  imediatamente, antes de cada item ser reproduzido, em vez de exibir apenas o
  caminho da URL.

## Localizacao

- Ajustados cabecalhos e entradas de catalogos gettext afetados por sincronizacao
  recente, preservando a base nativa em ingles via `lang/source.pot`.

# Fork WandersondeSouza - v7.1.2.5 (2026-05-31)

## Metadados do fork

- Atualizados os metadados do executavel e do instalador para identificar o
  projeto como `MPV.NET Media Player`.
- Atualizados produto, publicador do instalador, URLs de suporte/atualizacao e nome
  amigavel de associacoes de arquivo para apontar ao fork
  `WandersondeSouza/mpv.net`.
- Mantidos `mpvnet.exe`, `%APPDATA%\mpv.net` e chaves tecnicas de compatibilidade
  para preservar configuracoes e fluxos existentes.

## Localizacao e pacotes de idioma

- Validados os catalogos gettext em UTF-8 e corrigidos caracteres acentuados
  corrompidos em textos visiveis dos idiomas frances, polones e turco.
- Revalidada a paridade de todos os catalogos ativos com `lang/source.pot`:
  bulgaro, chines simplificado, espanhol, frances, alemao, italiano, japones,
  coreano, polones, portugues do Brasil, portugues de Portugal, russo e turco.
- Mantido o ingles como base nativa do aplicativo, sem criar pacote `.po`
  separado.

## Arquivos de midia e associacoes

- Reconfirmado o empacotamento de associacoes para video, audio e playlists no
  instalador x64.
- Documentado no release o escopo atual de video, audio, playlists e URLs de
  streaming aceitos pelo fork, preservando a compatibilidade com as listas de
  extensoes configuraveis do mpv.

# Fork WandersondeSouza - v7.1.2.4 (2026-05-29)

## Reproducao, arquivos e prioridade de entrada

- Ajustada a prioridade de reproducao para preservar melhor a compatibilidade
  com o mpv/libmpv ao abrir entradas locais, URLs e listas encaminhadas ao
  player.
- Reforcado o tratamento de associacoes de arquivos de video e playlists para
  o instalador registrar corretamente o `mpvnet.exe` como destino no Windows.
- Adicionada cobertura de teste para os cenarios de entrada relacionados a
  arquivos, URLs, playlists e prioridade de reproducao.

## Versionamento e pacote

- A versao do executavel voltou a ficar controlada apenas por
  `src/BuildVersion.props`, evitando divergencia entre o projeto Windows e a
  DLL principal.
- Removida a copia versionada de `MediaInfo.dll` em `src/Native/win-x64`; o
  fluxo de build/release passa a obter ou validar a DLL pelo script de
  dependencias nativas.

## Documentacao e consolidacao

- Consolidado no changelog e removido do plano de trabalho o que ja estava
  validado no fork: suporte a caminhos longos, reconhecimento de protocolos de
  streaming comuns, tratamento do estado de janela maximizada e a documentacao
  do fluxo de traducoes e configuracao local.

## Scripts de manutencao

- Renomeados os scripts PowerShell do fork para nomes mais objetivos, cobrindo
  build, release, dependencias nativas e localizacao gettext.
- Renomeado o script do Inno Setup para
  `src/Setup/Inno/build-windows-installer.iss`.
- Adicionado `docs/guia-operacional.md` com a funcao de cada script e exemplos de
  execucao a partir da raiz do repositorio.
- Atualizadas chamadas internas, alvo MSBuild, workflow de release e
  documentacao operacional para usar os novos nomes.

# Fork WandersondeSouza - v7.1.2.3 (2026-05-28)

## Associacoes de arquivos e streaming

- O instalador passou a registrar associacoes de video e playlists IPTV para o
  Windows encaminhar arquivos como `.mp4`, `.mkv`, `.m3u` e `.m3u8` ao
  `mpvnet.exe` apos a instalacao.
- A validacao de entrada de midia foi centralizada para aceitar arquivos locais,
  playlists e URLs de streaming sem tratar URLs como caminhos locais.
- O dialogo `Open Files...` passou a filtrar videos, playlists e a manter a
  opcao `All files (*.*)`.

## Localizacao

- Adicionada traducao inicial da interface para portugues brasileiro (`pt-BR`),
  selecionavel por `language=portuguese-brazil`.
- O modo `language=system` agora reconhece Windows em `pt-BR` e seleciona a
  traducao brasileira automaticamente.
- O editor de configuracao passou a traduzir textos visiveis da propria tela e
  a mostrar o idioma efetivo no combo quando `language=system`, preservando
  `system` no arquivo enquanto o usuario nao alterar o idioma manualmente.
- Os textos explicativos e ajudas das opcoes do editor de configuracao foram
  sincronizados em todos os catalogos gettext suportados.
- Builds Debug/Release do projeto Windows agora preparam automaticamente
  dependencias nativas/auxiliares e geram `Locale`, permitindo testar a
  traducao diretamente pelo Visual Studio.

## Idiomas e catalogos gettext

- Adicionados catalogos gettext para espanhol (`es`) e portugues de Portugal
  (`pt-PT`), mantendo o ingles como base nativa em `lang/source.pot`.
- Os catalogos ativos foram normalizados e sincronizados com `lang/source.pot`,
  incluindo entradas do editor de configuracao e textos visiveis da interface.
- O fluxo de geracao de `.mo` ganhou fallbacks locais para ambientes sem
  `msgfmt.exe`, reduzindo falhas durante build, teste e release.

## Reproducao, titulos e interface

- Ajustada a exibicao de titulos visiveis para manter nomes de midia
  normalizados na janela, OSD e playlist sem alterar a identidade real do
  arquivo ou URL reproduzido.
- Melhorada a compatibilidade de reproducao de arquivos web e URLs, preservando
  a delegacao ao mpv/libmpv.
- Corrigido o binding de `HintText` no campo de busca da janela de configuracao,
  evitando falha ao abrir o editor.

## Manutencao e documentacao

- Adicionada documentacao tecnica de localizacao do fork em
  `docs/developer/localization.md`.
- Adicionada a base inicial de wiki em `docs/wiki/Home.md`.
- Documentos e artefatos `.ai/` foram alinhados ao fluxo atual de manutencao,
  build x64, localizacao e release do fork.

# Fork WandersondeSouza - v7.1.2.2 (2026-05-22)

## Robustez interna

- Comandos de interface do `mpv.net` com argumentos ausentes ou invalidos agora
  registram erro controlado em vez de falharem por acesso direto a argumentos.
- Callbacks de propriedades vindos do libmpv passaram a ser copiados antes da
  execucao, reduzindo risco de reentrancia ou bloqueio durante eventos.
- A gravacao de `settings.xml` passou a usar arquivo temporario antes de
  substituir a configuracao final, reduzindo risco de arquivo parcial.

## Titulos visiveis de midia

- Os titulos visiveis de midia agora sao normalizados antes de aparecerem na
  janela, no OSD de reproducao e nas listas que usam `media-title`.
- A normalizacao automatica do titulo agora e aplicada por item de playlist,
  sem transformar o titulo limpo em identidade persistente para os proximos
  arquivos; o caminho/URL real continua sendo usado para carregar a midia.
- `--force-media-title` e valores literais de `--title` recebidos por linha de
  comando passam a ser ajustados antes de serem repassados ao mpv.
- O fallback `mpv.net` continua sendo usado quando nao ha midia/titulo, mas nao
  e concatenado ao titulo em reproducao.

## Release e alinhamento do pacote

- Adicionado `src/Tools/prepare-native-dependencies.ps1` para preparar a pasta
  do `mpvnet.exe` com `MediaInfo.dll`, `libmpv-2.dll`, FFmpeg, `yt-dlp.exe`,
  `mpvnet.com` e, quando houver publish self-contained, as DLLs Microsoft/.NET/WPF.
- O build Debug ganhou preparacao automatica de dependencias auxiliares para
  baixar ou validar esses arquivos quando necessario.
- O fluxo de release foi alinhado ao nome atual do asset do FFmpeg do BtbN:
  `ffmpeg-master-latest-win64-gpl.zip`.
- O script de release e a documentacao tecnica passaram a refletir o fluxo
  validado localmente para gerar o ZIP portatil e o instalador x64.
- O plano operacional do fork foi esvaziado e removido apos a consolidacao das
  pendencias ja tratadas, deixando o historico consolidado apenas no changelog
  e nas docs de release.

# Fork WandersondeSouza - v7.1.2.1 (2026-05-22)

## Identidade do fork, versao e release

- A janela `Help > About mpv.net` agora identifica este fork de manutencao,
  mostra o repositorio `https://github.com/WandersondeSouza/mpv.net` e preserva
  o credito ao projeto original mpv.net.
- A versao da aplicacao passou a ser centralizada em `src/BuildVersion.props`,
  usada pelo executavel e pelo fluxo de release para gerar artefatos e tag.
- A release x64 continua validando as dependencias nativas obrigatorias no
  publish, na pasta portatil e no ZIP, incluindo `MediaInfo.dll`,
  `libmpv-2.dll` e as DLLs Microsoft/.NET/WPF vindas do publish self-contained.
- O script de release foi atualizado para o nome atual dos assets FFmpeg da
  BtbN e para tratar beta/pre-release apenas quando a versao informacional
  trouxer marcador explicito.

# Fork WandersondeSouza - alteracoes recentes (2026-05-21)

## Release, pacote portatil e dependencias nativas

- O fluxo de release passou a publicar self-contained `win-x64`, preservando as
  DLLs nativas Microsoft/.NET/WPF no output final.
- Adicionados scripts para baixar MediaInfo da MediaArea oficial, validar DLLs
  x64 e bloquear pacote sem dependencias obrigatorias.
- O workflow manual `.github/workflows/release-packages.yml` passou a gerar
  pacotes no GitHub Actions e, quando iniciado com `create_release=true`,
  tambem cria uma GitHub Release com os assets gerados.
- O pacote portatil passou a incluir `portable_config/` com exemplos iniciais de
  `mpv.conf`, `input.conf`, `scripts/` e `script-opts/`.
- FFmpeg, libmpv, MediaInfo, `yt-dlp.exe`, `mpvnet.com` e `Locale` passaram a
  ser preparados pelo fluxo de build/release do fork.
- O build da aplicacao Windows foi ajustado para `win-x64`/64 bits de forma
  explicita, alinhando projeto, instalador e documentacao.

## Manutencao do aplicativo e documentacao tecnica

- Adicionada documentacao inicial do fork em portugues brasileiro, incluindo
  README de manutencao, guia de contribuicao, roadmap e documentos tecnicos em
  `docs/developer/`.
- Adicionados artefatos de IA em `.ai/` com skill, agentes, prompts e guia MCP
  para manutencao conservadora do fork.
- Adicionados exemplos de configuracao em `docs/exemplos/`, incluindo
  `mpv.conf`, `input.conf`, `thumbfast.conf` e exemplos para `portable_config`.
- Documentados atalhos, caminhos longos no Windows, `thumbfast` no modo
  portatil e o perfil `[iptv-media-center]` usado sob demanda.
- Corrigido o carregamento de arquivos locais com caminho acima de 260
  caracteres, normalizando o caminho para o formato estendido `\\?\` antes de
  entregar ao mpv/libmpv.

## Observacao sobre GitHub Packages

- Este fork, por enquanto, nao publica pacote NuGet/container no GitHub
  Packages. Os artefatos de distribuicao ficam em GitHub Releases ou como
  artefatos de workflow.

