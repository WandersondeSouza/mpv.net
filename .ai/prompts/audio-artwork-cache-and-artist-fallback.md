# Capas online de áudio, cache e foto do artista como fallback

Prompt incremental para implementar melhorias sobre a resolução de arte de
áudio já existente. Este arquivo é um plano de trabalho futuro; confirme o
estado real na branch antes de editar e não repita comportamento que já esteja
consolidado.

## Objetivo

Tornar a busca online de capas de áudio controlável pelo painel de configuração,
com padrão ativo; conservar e limpar o cache dedicado de arte usando a retenção
e os limites existentes; permitir o uso offline de arte já armazenada; e
oferecer foto licenciada do artista somente como fallback quando nenhuma capa
de álbum válida estiver disponível. Falhas de rede, metadados, licença ou
imagem nunca devem atrasar nem interromper a reprodução.

## Leitura e auditoria obrigatórias

Antes de alterar código, leia `AGENTS.md`, `README.md`,
`.ai/skills/mpvnet-maintainer.md`, `docs/manual.md`,
`docs/guia-operacional.md`, `docs/developer/configuration.md`,
`docs/developer/windows-ui.md`, `docs/developer/mpv-integration.md` e
`docs/developer/localization.md`. Inspecione também o prompt anterior
`.ai/prompts/audio-artwork-embedded-local-online.md`, sem tratá-lo como prova do
estado atual.

Confirme no código atual, incluindo `App.ProcessProperty`,
`Resources/editor_conf.txt`, `MainForm.AudioArtwork`, o resolver, os clientes
online, `AudioArtworkCache`, `AppPaths` e `src/MpvNet.Tests/`, pelo menos estes
pontos:

- A opção `audio-artwork-online` já é lida de `mpvnet.conf`, mas hoje o valor
  inicial é `no` e a opção não está exposta no editor de configuração.
- Capas locais/incorporadas e capas de álbum online já podem ser resolvidas.
  Foto separada do artista ainda não está implementada.
- O cache dedicado atual fica em `AppPaths.LocalRoot\Artwork`. Ele já usa
  gravação atômica, limite de 128 MiB, 512 arquivos e limpeza por idade de 30
  dias quando grava. A limpeza geral ao iniciar cobre `Cache` e `Temp` com
  retenção de três dias, não a pasta `Artwork`.
- A resolução online já ocorre em segundo plano e possui timeout, cancelamento
  e tratamento de falhas. Confirme esses limites no código e amplie a cobertura
  sem presumir que o comportamento está completo.
- Preserve a preferência existente `audio-artwork-prefer-online`, as escolhas
  explícitas do mpv/CLI, o comportamento de `audio-display=no`, a arte local e
  incorporada, a proteção de vídeo real e os controles do Windows.

Confira o estado do Git antes das edições e preserve alterações preexistentes.
No estado conhecido deste plano, há uma alteração não relacionada em
`src/MpvNet.Pacote/Package.appxmanifest`; não a reverta nem a inclua no escopo.

## Requisitos de implementação

### Opção no painel

- Reutilize a chave compatível `audio-artwork-online`; não crie outra chave.
- Adicione uma opção clara de Sim/Não no editor de configuração, na categoria
  de áudio, com padrão `yes`. Faça o valor padrão em runtime também ser ativo,
  inclusive quando a opção não aparece em um `mpvnet.conf` antigo.
- Um `audio-artwork-online=no` explícito continua desativando consultas e
  downloads novos. Desativar a rede não desativa leitura de cache, arte local,
  arte incorporada nem escolhas nativas do mpv.
- Ao desligar a opção, capas online e fotos de artista já validadas e presentes
  no cache podem continuar sendo usadas sem rede. Garanta por teste que nenhum
  pedido HTTP seja iniciado nesse modo, inclusive em caso de cache miss.
- `audio-artwork-prefer-online` continua separado: sozinho não autoriza rede e
  mantém o significado atual para a prioridade entre capas de álbum.
- Atualize o help do painel e explique que a opção pode enviar identificadores
  ou metadados do artista/álbum e o endereço IP aos serviços consultados.

### Cache e limpeza ao iniciar

- Reutilize `AppPaths.LocalRoot\Artwork` e o `AudioArtworkCache` existente;
  não migre a pasta para `AppPaths.Cache`, pois a limpeza global dessa pasta
  remove arquivos com mais de três dias.
- Mantenha a retenção de 30 dias e os limites existentes de 128 MiB e 512
  arquivos para imagens e metadados gerenciados pelo cache.
- Acrescente uma limpeza dedicada ao iniciar o player, antes de a arte ser
  necessária. Remova itens vencidos com mais de 30 dias e aplique os mesmos
  limites de tamanho/quantidade. A limpeza deve ser idempotente, tolerar arquivo
  bloqueado, sem permissão ou diretório ausente, e não pode percorrer ou apagar
  configurações, mídia ou arquivos fora da pasta dedicada.
- A limpeza deve funcionar offline e mesmo quando a busca online estiver
  desativada. Use o mesmo mecanismo de cache para capas de álbum e fotos de
  artista, com chaves namespaced por tipo de entidade para evitar colisões.
- Para fotos do Commons, persista metadados suficientes para validar o cache
  offline: identidade do artista, origem, título/URL da página do arquivo,
  licença reconhecida, data e hash da imagem. Não armazene caminhos locais da
  mídia como identidade do cache.

### Foto do artista como último fallback

- A prioridade é: escolhas explícitas do usuário/mpv; resolução de capa de
  álbum conforme as regras atuais; foto verificada do artista; fallback padrão
  do aplicativo. A foto do artista nunca substitui capa de álbum válida,
  incorporada, local ou online.
- Prefira `MusicBrainz Artist Id` presente nas tags. Sem MBID, aceite somente
  uma correspondência única e estrita baseada nos metadados do artista ou
  artista do álbum. Rejeite artistas múltiplos/ambíguos, “Various Artists”,
  metadados insuficientes e resultados baseados apenas no nome do arquivo.
- Resolva a entidade pelo MusicBrainz, siga apenas um vínculo Wikidata
  verificável e consulte a imagem `P18` da entidade correspondente. Obtenha o
  arquivo e os metadados de licença/autoria por APIs oficiais do Wikimedia
  Commons. Não faça scraping nem busca genérica por imagens.
- Aceite somente domínio público ou CC0 identificados sem ambiguidade nos
  metadados da licença. Se a licença, autoria, identidade ou destino não puder
  ser confirmado, descarte a foto e use o próximo fallback; não aceite
  silenciosamente CC BY, CC BY-SA ou licença ausente. Preserve metadados de
  autoria/origem quando disponíveis para rastreabilidade, sem exigir uma nova
  interface de créditos para imagens PD/CC0.
- Confirme a documentação vigente dos serviços antes de implementar. Use
  HTTPS, User-Agent descritivo do produto, limites de chamadas, timeout,
  cancelamento, limite de bytes/dimensões e validação estrita dos hosts e de
  cada redirecionamento. Respeite `Retry-After` e não faça requisições
  concorrentes desnecessárias. Referências oficiais:
  - [MusicBrainz e Wikidata](https://musicbrainz.org/doc/Wikidata)
  - [Wikibase API](https://www.mediawiki.org/wiki/Wikibase/API)
  - [Dados legíveis por máquina do Commons](https://commons.wikimedia.org/wiki/Help:Machine-readable_data)
  - [Política de acesso às APIs Wikimedia](https://www.mediawiki.org/wiki/Wikimedia_APIs/Access_policy)
- Não acrescente dependência externa sem necessidade demonstrada. Não grave
  imagens junto aos arquivos de áudio nem altere tags ou arquivos do usuário.

### Reprodução resiliente e apresentação

- Toda consulta de rede, decodificação e escrita deve permanecer fora do thread
  da UI e do caminho síncrono de carregamento/controle do mpv.
- Uma falha, resposta inválida, ausência de conexão, rate limit, timeout,
  cancelamento ou licença recusada deve produzir fallback e log seguro, sem
  expor caminhos, URLs com credenciais ou tags completas.
- Troca rápida de faixa, fechamento do player e encerramento devem cancelar ou
  invalidar resultados tardios. Nunca publicar uma foto/capa da faixa anterior.
- Use a mesma imagem resolvida na janela do player e no SMTC quando o adapter
  do MPV.NET estiver ativo. Não crie uma sessão SMTC duplicada quando o usuário
  habilitar os controles nativos do libmpv.
- Preserve vídeo real, seleção manual de faixa, fullscreen, OSC, configurações
  de mpv, fallback atual e o comportamento de URLs/streams, credenciais,
  drives de rede e arquivos offline.

### Documentação e localização

- Atualize `docs/manual.md` para mostrar o controle no painel, o padrão ativo,
  como desativar, o uso de imagens existentes em cache e os serviços/dados
  envolvidos na consulta de capa e foto.
- Atualize os documentos técnicos existentes de configuração e UI/integração
  após consolidar o comportamento. Não crie documentação técnica redundante.
- Traduza rótulo e help do editor usando o fluxo gettext do projeto:
  `lang/source.pot` e todos os catálogos `lang/po/*.po` atuais. Preserve nomes
  de opções, placeholders e valores literais.
- Verifique links locais, caminhos citados, paridade dos catálogos e
  `git diff --check`.

## Testes e critérios de aceite

Use testes determinísticos com handlers/servidor HTTP local; a suíte não pode
depender da internet real. Amplie os testes por área em `src/MpvNet.Tests/` e
`Program.cs` quando aplicável, cobrindo:

1. Padrão ativo no runtime/editor, persistência de `yes`/`no` e precedência de
   configuração explícita antiga.
2. Opção desligada sem requisições de rede, mas com leitura de capas/fotos já
   cacheadas e resolução de arte local/incorporada.
3. Limpeza ao iniciar: itens com mais de 30 dias removidos, itens recentes
   mantidos, limites de 128 MiB/512 arquivos aplicados, falha de exclusão
   tolerada e conteúdo externo à pasta preservado.
4. Identificação MusicBrainz/Wikidata única, P18, hosts/redirecionamentos,
   licenças PD/CC0 aceitas, licença desconhecida/ambígua recusada, ausência,
   JSON inválido, imagem inválida/grande e cache hit offline.
5. Capa de álbum sempre superior à foto do artista; preferência existente por
   arte online não deve promover foto de artista acima de uma capa válida.
6. Offline, timeout, 404, 429 com `Retry-After`, falha de decodificação,
   cancelamento, mudança rápida de faixa e fechamento sem travar UI ou áudio.
   Inclua evidência de que a reprodução continua avançando durante falha/rede
   lenta, por teste de aplicação/IPC quando viável.

Execute a suíte conforme `docs/guia-operacional.md`:

```powershell
dotnet run --project .\src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore
dotnet build .\src\MpvNet.Windows\MpvNet.Windows.csproj
```

Valide o painel com tema claro/escuro, DPI, idioma, filtro/busca, valor salvo e
reabertura. Faça o checklist manual do projeto para arquivo local, URL/stream,
playlist, pasta com mídia, drag/drop, menu de contexto, fullscreen, alternância
de faixa/legenda, cursor/OSC, comandos da janela e fechamento. Examine a janela
e o SMTC separadamente. Informe o que foi validado localmente e o que depender
de serviço externo, instalação MSIX ou outro ambiente.

## Restrições e entrega

- Faça a menor mudança compatível; não refatore amplamente o fluxo de áudio,
  não altere comportamento de configuração mpv não relacionado e não adicione
  dependência sem justificativa.
- Não reverta alterações preexistentes, não faça commit/push/publicação e não
  apresente teste local como prova de disponibilidade contínua dos serviços.
- Antes de editar, apresente resumo do entendimento, arquivos envolvidos,
  problema, mudança, riscos e plano de teste conforme `AGENTS.md`.
- Ao concluir, informe arquivos alterados, decisões de prioridade/identidade,
  resultados dos testes/build, validação manual, limites e pendências.
