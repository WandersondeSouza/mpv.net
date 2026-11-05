# Capas de áudio incorporadas, locais e online

Implementação consolidada em 05/11/2026: capas locais/incorporadas via imagem
selecionada pelo mpv, busca online opcional de álbum, cache e fallback estão
descritos no [manual](../../docs/manual.md#capas-durante-a-reprodução-de-áudio)
e na [integração Windows](../../docs/developer/windows-ui.md). A alternativa
adotada reutiliza os pixels do libmpv e não acrescenta parser/dependência de
tags. Foto automática do artista segue condicionada à confirmação de fonte,
identidade e direitos; não está habilitada. O texto abaixo conserva o plano
para auditorias futuras, não substitui a documentação do comportamento atual.

## Objetivo

Melhorar a imagem apresentada durante a reprodução de áudio no MPV.NET, tanto
na janela do player quanto nos controles de mídia do Windows (SMTC). Aproveitar
primeiro a arte incorporada no arquivo ou uma imagem local; oferecer, como
melhoria opcional, busca online de capa do álbum e eventual foto do artista
quando não houver uma imagem adequada. A reprodução deve continuar mesmo se
qualquer fonte de imagem falhar.

Este é um plano para execução futura, não uma descrição garantida do estado
atual. Ao retomá-lo, confirme cada comportamento na branch e registre o que já
está implementado, o que falta e o que deixou de fazer sentido.

## Contexto a confirmar no código

- O mpv oferece `audio-display`, `cover-art-auto` e capas incorporadas/externas
  como faixas de imagem. Preserve as opções explícitas em `mpv.conf`, na linha
  de comando e por arquivo; não imponha outra prioridade silenciosamente.
- `MainForm.MediaTransport.cs` já consulta tags e
  `current-tracks/video/albumart`/`external-filename`. A imagem selecionada
  pelo mpv para a janela e a publicada no SMTC são caminhos distintos.
- `MediaTransportThumbnailLoader.cs` aceita capa externa local ou miniatura
  fornecida pelo Windows com `ReturnOnlyIfCached`; não extrai diretamente a
  imagem incorporada nem busca arte na rede. O registro em
  `docs/developer/windows-ui.md` relata uma amostra incorporada sem miniatura.
- `MediaInfo.cs` expõe principalmente texto. Não presuma que o wrapper atual
  devolva os bytes da imagem incorporada.
- O frontend desativa seu adapter SMTC quando `media-controls=yes` é definido
  explicitamente para o libmpv. Respeite essa escolha.

Fontes para revalidar opções e serviços: [manual do mpv](https://mpv.io/manual/master/),
[API do MusicBrainz](https://musicbrainz.org/doc/MusicBrainz_API) e
[API do Cover Art Archive](https://musicbrainz.org/doc/Cover_Art_Archive/API).

## Leitura obrigatória antes de implementar

Leia `AGENTS.md`, `README.md`, `.ai/skills/mpvnet-maintainer.md`,
`docs/manual.md`, `docs/guia-operacional.md`,
`docs/developer/mpv-integration.md`, `docs/developer/windows-ui.md`,
`docs/developer/configuration.md` e o código relacionado em
`src/MpvNet/Integration/Mpv/`, `src/MpvNet.Windows/WinForms/`,
`src/MpvNet.Windows/Services/MediaTransport/` e `src/MpvNet.Tests/`.
Verifique o estado Git e preserve alterações locais. Antes de editar, apresente
entendimento atual, arquivos envolvidos, problema, mudança pequena proposta,
riscos e plano de teste.

## Trabalho proposto

### 1. Determinar as fontes de imagem e a prioridade

- Reproduza arquivos MP3, FLAC, M4A e ao menos outro formato suportado com e
  sem arte incorporada; teste uma capa na pasta, duas imagens candidatas,
  arquivo com imagem inválida e arquivo cujo mpv não ofereça faixa `albumart`.
- Diferencie: imagem renderizada pelo mpv na janela, imagem exibida no SMTC e
  ícone usado quando nenhuma imagem é válida. Não trate sucesso em uma
  superfície como prova de sucesso na outra.
- Defina uma ordem explícita e documentada, em princípio: escolha do usuário
  ou opção explícita do mpv; arte incorporada/externa segundo `audio-display`;
  capa online identificada com confiança; foto do artista, se houver fonte e
  direitos adequados; imagem padrão do aplicativo. Permita escolher uma capa
  local ou preferir arte online quando a incorporada for ruim, sem alterar
  arquivos de mídia ou tags.
- Quando houver vídeo real, não o substitua por imagem de álbum. Respeite
  `audio-display=no`, seleção manual de faixa e outras opções do mpv.

### 2. Resolver a arte local com segurança

- Reutilize a faixa de capa já escolhida pelo mpv sempre que possível. Para
  publicar arte incorporada no SMTC, avalie uma extração direta e limitada dos
  bytes do arquivo, com biblioteca/formato apropriado, apenas se o mpv/Windows
  não oferecer um caminho confiável. Compare custo, licenças e empacotamento
  antes de adicionar dependências. Não use miniatura Windows em cache como
  prova de que a arte incorporada sempre será obtida.
- Valide tipo, tamanho em bytes, dimensões e decodificação; copie a imagem
  para memória/cache próprio antes de liberar o arquivo de origem. Preserve a
  política atual para mídia remota, drives de rede e arquivos offline/reparse;
  nenhuma extração de arte deve hidratar arquivo de nuvem ou bloquear a UI.
- Se uma capa local for acrescentada durante a reprodução, verifique se o
  comando nativo do mpv para carregar/reexaminar capas resolve o caso sem
  reiniciar a faixa. Evite substituir `wid`, desenhar sobre vídeo real ou
  alterar o OSC para implementar a imagem.

### 3. Buscar na internet somente quando habilitado

- Proponha uma opção de usuário clara, desligada por padrão, para busca
  automática online. Mantenha o modo atual offline como padrão. Separe a
  preferência de prioridade da permissão de consultar serviços externos.
- Use primeiro identificadores MusicBrainz presentes nas tags, quando válidos;
  na ausência deles, pesquise por artista e álbum/título e aceite apenas
  correspondência suficientemente confiável. Não use apenas o nome do arquivo
  ou um título de URL como identidade. Não consulte serviços para vídeo,
  streams/URLs com credenciais ou mídia sem metadados mínimos confiáveis.
- Avalie MusicBrainz para identificação e Cover Art Archive para capa de
  release/release-group. Siga a identificação por User-Agent, o limite de
  chamadas e as condições atuais dos serviços. Considere 404 como ausência
  normal. Não faça scraping de busca genérica de imagens.
- Foto do artista é uma etapa separada: selecione uma fonte documentada,
  confirme identidade do artista, licença/atribuição e disponibilidade da
  imagem. Não invente uma foto nem use resultado ambíguo. Se esse caminho não
  puder ser sustentado, entregue apenas capas de álbum e imagem padrão.
- Faça chamadas HTTPS assíncronas com timeout, cancelamento, limite de
  redirecionamentos, tamanho e dimensões. Aceite somente hosts esperados e
  valide o destino de cada redirecionamento. Falha de rede nunca deve atrasar
  áudio, troca de faixa ou encerramento.
- Guarde miniaturas validadas no cache de dados do aplicativo, respeitando
  portátil/instalado/MSIX; defina chave por identidade de álbum, validade,
  limite de espaço e limpeza. Evite consultas repetidas ao mudar entre faixas
  do mesmo álbum e evite repetir 404 a cada reprodução. Não grave capa baixada
  na pasta do usuário sem uma ação explícita.

### 4. Exibir e sincronizar

- Compare adicionar a capa ao mpv como faixa `albumart` com uma superfície de
  UI separada. Escolha a menor abordagem que preserve vídeo real, seleção de
  faixas, OSC, fullscreen, DPI, temas, drag/drop e atalhos. Prove o resultado
  com libmpv empacotado antes de consolidar a escolha.
- Faça o SMTC receber a mesma imagem resolvida quando o adapter do frontend
  estiver ativo, sem republicar bytes a cada atualização de posição. Cancele
  resultados de uma faixa anterior e descarte streams/bitmaps ao trocar de
  mídia ou fechar. Com `media-controls=yes`, não crie segunda sessão SMTC.
- Mantenha um fallback visual adequado quando não houver capa; não deixe
  imagem da faixa anterior na tela nem no painel do Windows.

## Etapas e critérios de aceite

1. **Arte local:** capa incorporada e capa externa aparecem corretamente nas
   superfícies suportadas; ausência, corrupção e troca de faixa retornam ao
   fallback; configurações explícitas do mpv continuam válidas.
2. **Busca online opcional:** apenas quando habilitada, uma correspondência
   confiável mostra a capa na janela e, se aplicável, no SMTC; offline, 404,
   limite de chamadas e mudança rápida de faixa não afetam a reprodução.
3. **Foto do artista:** só implemente depois de confirmar fonte, identidade,
   licença e atribuição. Caso contrário, registre a limitação e mantenha a
   imagem padrão.

Para cada etapa, amplie testes em `src/MpvNet.Tests/` para seleção/prioridade,
metadados incompletos, imagem inválida/grande, cache, cancelamento, resultado
tardio e limpeza de recursos. Use servidor HTTP de teste local para simular
sucesso, 404, timeout, redirecionamento e resposta indevida; os testes não
devem depender da internet real. Rode a suíte e o build Windows conforme
`docs/guia-operacional.md`. Valide manualmente áudio local, URL/stream,
playlist, pasta, drag/drop, menu de contexto, fullscreen, troca de faixas,
cursor/OSC, tema claro/escuro, DPI, comandos de janela e encerramento. Confira
separadamente o painel de mídia do Windows e a janela do player em execução
portátil e MSIX quando disponíveis. Não apresente teste local como prova da
Microsoft Store ou de serviços externos reais.

Atualize `docs/manual.md` para opções e privacidade, e os documentos técnicos
existentes de UI, mpv e configuração apenas após consolidar o comportamento.
Não crie documentação redundante. Entregue a lista de arquivos alterados,
decisões de prioridade e fonte, testes com resultados, validação manual feita,
limitações e pendências por ambiente.
