# MODERNIZAÇÃO DE MÍDIA ONLINE, YOUTUBE, PLAYLISTS E STREAMING — MPV.NET MEDIA PLAYER

Trabalhe no repositório atual **MPV.NET Media Player** (`WandersondeSouza/mpv.net`).

O objetivo desta tarefa é modernizar e tornar robusto todo o fluxo de **entrada e reprodução de mídia online**, com atenção especial a **URLs do YouTube**, **playlists do YouTube**, **yt-dlp**, **streaming ao vivo**, **cache de rede**, **IPC/segunda instância**, **desempenho** e uma **auditoria arquitetural final**, sem quebrar a compatibilidade existente com mpv/libmpv, arquivos locais, playlists tradicionais, comandos, configurações ou integrações já implantadas.

Este trabalho deve implementar, de forma coordenada e incremental, os seguintes pontos priorizados:

1. corrigir o tratamento de URLs do YouTube com parâmetros adicionais;
2. suportar playlists do YouTube preenchendo automaticamente a fila de reprodução;
3. criar uma camada central de normalização de entradas de mídia;
4. melhorar a integração, validação, diagnóstico e atualização do `yt-dlp`;
5. melhorar mensagens e diagnóstico de erros de streaming;
6. melhorar resiliência e reconexão controlada de transmissões ao vivo;
7. aprimorar a política de cache por tipo de mídia/rede;
8. revisar integralmente o fluxo de segunda instância/IPC para URLs e playlists;
9. criar testes automatizados completos para URLs e combinações de parâmetros;
10. revisar desempenho do fluxo de reprodução e reduzir trabalho repetido/desnecessário;
11. executar uma auditoria arquitetural e de qualidade final de tudo que foi alterado.

---

# REGRA PRINCIPAL

Este trabalho DEVE ser realizado **por etapas pequenas, verificáveis e independentes**.

NÃO implemente tudo de uma vez.

Para CADA etapa:

1. analisar o comportamento atual;
2. registrar evidências do problema ou da oportunidade de melhoria;
3. identificar os arquivos realmente envolvidos;
4. definir riscos antes de editar;
5. implementar a menor mudança coerente possível;
6. criar ou ampliar testes automatizados;
7. compilar;
8. executar os testes relevantes;
9. executar testes de regressão relacionados;
10. revisar logs e comportamento manual quando necessário;
11. revisar o diff;
12. fazer um commit pequeno e descritivo;
13. fazer push;
14. somente então iniciar a próxima etapa.

Se uma etapa crescer demais, subdivida antes de continuar.

Não faça refatorações grandes apenas por preferência estética.

Não substitua comportamentos nativos do mpv/yt-dlp por implementações próprias em C# quando o mpv/yt-dlp já fornecer a função corretamente.

---

# LEITURA OBRIGATÓRIA

Antes de qualquer alteração, leia obrigatoriamente:

- `AGENTS.md`
- `.ai/skills/mpvnet-maintainer.md`
- `.ai/agents/mpvnet-architecture-agent.md`
- `.ai/agents/mpvnet-libmpv-agent.md`
- `README.md`
- `docs/manual.md`
- `docs/guia-operacional.md`
- `docs/developer/architecture.md`
- `docs/developer/mpv-integration.md`
- `docs/developer/configuration.md`
- `.ai/prompts/playback-reliability-deep-audit.md`

O prompt `playback-reliability-deep-audit.md` já cobre profundamente interop, lifetime de handles, memória nativa, eventos e confiabilidade do núcleo. Não duplique alterações já feitas ou planejadas ali sem necessidade concreta para este escopo.

Analise obrigatoriamente, entre outros que forem descobertos:

- `src/MpvNet/Media/MediaInput.cs`
- `src/MpvNet/Media/PlaylistFile.cs`
- `src/MpvNet/Configuration/CommandLine.cs`
- `src/MpvNet/Integration/Mpv/Player.MediaLoading.cs`
- `src/MpvNet/Integration/Mpv/Player.Initialization.cs`
- `src/MpvNet/Integration/Mpv/Player.Events.cs`
- `src/MpvNet/Integration/Mpv/Player.State.cs`
- `src/MpvNet/Integration/Mpv/Player.cs`
- `src/MpvNet/Infrastructure/RuntimeComponents/*`
- `src/MpvNet/RuntimeComponents.cs`
- `src/MpvNet.Windows/Program.cs`
- `src/MpvNet.Windows/WinForms/MainForm.DragDrop.cs`
- `src/MpvNet.Windows/WinForms/MainForm.PlayerEvents.cs`
- `src/MpvNet.Windows/WinForms/MainForm.MediaTransport.cs`
- testes existentes em `src/MpvNet.Tests`

Antes de editar, produzir obrigatoriamente:

```text
Resumo do entendimento atual:
Arquivos envolvidos:
Fluxos de entrada existentes:
Problema encontrado:
Mudança proposta:
Riscos:
Plano de teste:
Critérios de aceite:
```

---

# ETAPA 0 — BASELINE E MAPA DO FLUXO ATUAL

Antes de modificar código:

- confirmar branch atual;
- confirmar working tree limpo;
- registrar commit inicial;
- restaurar dependências;
- compilar a solução;
- executar a suíte de testes existente;
- registrar warnings e falhas já existentes;
- não atribuir à nova implementação problemas anteriores.

Executar pelo menos:

```text
dotnet restore src\MpvNet.sln
dotnet build src\MpvNet.sln
dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore
dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore
```

Mapear claramente todos os caminhos pelos quais uma mídia pode entrar no sistema:

- linha de comando;
- abertura pelo Windows / associação de arquivo ou protocolo;
- clipboard;
- drag-and-drop;
- diálogo de arquivo/URL, se existir;
- playlist;
- recentes;
- IPC / segunda instância;
- comando interno;
- extensões/plugins, se aplicável.

Documentar onde cada fluxo passa até chegar a `loadfile`/mpv.

---

# ETAPA 1 — CAMADA CENTRAL DE NORMALIZAÇÃO DE ENTRADA

Criar ou consolidar uma camada central de entrada, com nome coerente com a arquitetura atual, por exemplo:

```text
MediaInputNormalizer
```

ou outra abstração equivalente melhor justificada.

Objetivo:

**toda entrada de mídia deve passar pela mesma política de normalização e validação antes de chegar ao player.**

Essa camada deve:

- preservar URLs válidas integralmente;
- remover apenas aspas externas ou artefatos comprovadamente inválidos;
- nunca truncar query string;
- nunca interpretar `&`, `?`, `=`, `#`, `%` ou parâmetros de URL como argumentos independentes;
- preservar Unicode;
- preservar encoding válido;
- distinguir arquivo local de URI;
- distinguir URL de rede de caminho Windows;
- não transformar `https://` em caminho local;
- não alterar parâmetros do YouTube por conveniência;
- rejeitar apenas entradas comprovadamente inválidas ou inseguras;
- produzir resultado determinístico independentemente da origem.

Evitar lógica específica de UI nessa camada.

A origem deve continuar disponível através de `MediaInputSource` ou estrutura equivalente.

Garantir que clipboard, command line, IPC e drag/drop utilizem o mesmo pipeline.

---

# ETAPA 2 — CORRIGIR URLS DO YOUTUBE COM PARÂMETROS

Investigar e corrigir o problema onde URLs do YouTube deixam de funcionar quando possuem parâmetros concatenados.

Cobrir, no mínimo:

```text
https://www.youtube.com/watch?v=VIDEO_ID
https://www.youtube.com/watch?v=VIDEO_ID&t=120s
https://www.youtube.com/watch?v=VIDEO_ID&start=120
https://www.youtube.com/watch?v=VIDEO_ID&list=PLAYLIST_ID
https://www.youtube.com/watch?v=VIDEO_ID&list=PLAYLIST_ID&index=3
https://www.youtube.com/watch?v=VIDEO_ID&pp=...
https://youtu.be/VIDEO_ID
https://youtu.be/VIDEO_ID?si=...
https://youtu.be/VIDEO_ID?t=45
https://youtu.be/VIDEO_ID?si=...&t=45
```

Também testar combinações contendo:

- `list`;
- `index`;
- `t`;
- `start`;
- `si`;
- `pp`;
- fragmento `#`;
- parâmetros desconhecidos futuros.

Regra obrigatória:

**não remova parâmetros apenas porque o código atual não os conhece.**

Preservar a URL completa e delegar ao mpv/yt-dlp a interpretação quando apropriado.

Investigar especialmente:

- `CommandLine.IsLoadableFileArgument`;
- parsing de argumentos;
- passagem pela segunda instância;
- serialização IPC;
- `ConvertFilePath`;
- `BuildLoadfileArgs`;
- comandos `loadfile`;
- tratamento de aspas do Windows;
- qualquer `Split`, `Substring`, `IndexOf`, regex ou concatenação que possa truncar URLs.

---

# ETAPA 3 — PLAYLISTS DO YOUTUBE E FILA DE REPRODUÇÃO

Quando a URL contiver uma playlist do YouTube (`list=...`), o comportamento esperado é:

1. preservar a URL completa;
2. permitir que mpv + yt-dlp resolvam a playlist;
3. carregar automaticamente os demais vídeos na playlist/fila interna do mpv;
4. respeitar `index=` quando fornecido;
5. manter como item atual o vídeo solicitado pela URL;
6. permitir próximo/anterior normalmente;
7. avançar automaticamente ao terminar um item;
8. evitar itens duplicados;
9. não reiniciar o vídeo atual apenas porque a playlist foi expandida;
10. não criar manualmente em C# uma sequência de URLs individuais se o mpv/yt-dlp já resolver corretamente a playlist.

Exemplo esperado:

```text
URL:
https://www.youtube.com/watch?v=VIDEO3&list=PLAYLIST123&index=3

Fila resultante:
Vídeo 1
Vídeo 2
Vídeo 3  <- atual
Vídeo 4
Vídeo 5
...
```

Investigar propriedades/eventos do mpv para saber quando a playlist foi expandida.

Não assumir que todos os itens estarão disponíveis imediatamente após `loadfile`.

Validar também:

- playlist com vídeo removido;
- vídeo privado;
- playlist muito grande;
- playlist que inicia em índice diferente de 1;
- playlist contendo shorts;
- URL de playlist sem `watch?v=`;
- URL de vídeo sem `list=` continua carregando apenas o vídeo solicitado.

Se houver opção nativa do mpv/yt-dlp que controla playlist (`ytdl`, `ytdl-raw-options`, `playlist-start`, `playlist-end`, etc.), verificar documentação atual antes de alterar comportamento.


---

# ADENDO OBRIGATÓRIO 2026 — PLAYLISTS YOUTUBE, EJS, RUNTIME JAVASCRIPT E DIAGNÓSTICO MODERNO

> Este adendo faz parte obrigatória deste prompt e prevalece sobre qualquer suposição anterior conflitante neste documento.
> Antes de implementar, o Codex DEVE consultar a documentação oficial atual do mpv e do yt-dlp e registrar as versões efetivamente usadas na validação.

## A. Política real de playlists do YouTube

NÃO assumir que uma URL `watch?v=...&list=...` será automaticamente expandida para a playlist completa apenas por ser entregue ao mpv.

Auditar obrigatoriamente o comportamento atual do `ytdl_hook` do mpv e as opções repassadas ao yt-dlp.

Na implementação atualmente documentada pelo mpv, o fluxo pode usar comportamento equivalente a `--no-playlist` por padrão, salvo quando opções apropriadas habilitarem playlist, por exemplo via `ytdl-raw-options` / `yes-playlist`.

Portanto, definir e testar explicitamente uma política de intenção:

```text
URL claramente de vídeo individual
    -> reproduzir somente o item, salvo intenção explícita de coleção

URL claramente de playlist/coleção
    -> permitir expansão nativa via mpv + yt-dlp

URL watch?v=VIDEO&list=PLAYLIST
    -> preservar a URL completa
    -> determinar a intenção conforme a entrada e a política do player
    -> habilitar expansão quando o objetivo for playlist
```

Não criar uma playlist paralela em C# se mpv + yt-dlp puderem realizar a expansão corretamente.

Não transformar todo vídeo que contém contexto de playlist em coleção sem validar a intenção e o comportamento atual.

## B. `index=` e posição do item atual

O requisito funcional continua sendo:

```text
https://www.youtube.com/watch?v=VIDEO3&list=PLAYLIST123&index=3
```

deve resultar, quando a playlist for solicitada, na playlist expandida com o item correspondente selecionado.

Porém, NÃO assumir que `index=3` será automaticamente refletido em `playlist-pos` pela combinação atual de mpv + yt-dlp.

O Codex deve:

1. validar o comportamento real da versão atual;
2. observar a playlist nativa do mpv após expansão;
3. confirmar se o item corrente corresponde ao vídeo indicado;
4. somente se necessário, sincronizar a posição usando propriedades/comandos nativos do mpv;
5. evitar reconstruir a playlist manualmente;
6. evitar reiniciar o vídeo atual durante a sincronização;
7. adicionar testes para posição inicial, próximo, anterior e avanço automático.

## C. Cadeia moderna de reprodução online

Não considerar mais `yt-dlp.exe presente e atualizado` como diagnóstico suficiente para declarar a integração YouTube saudável.

Auditar a cadeia completa:

```text
MPV.NET
  -> mpv/libmpv
     -> ytdl_hook
        -> yt-dlp
           -> suporte a desafios JavaScript quando necessário
              -> EJS / componente equivalente oficialmente suportado
              -> runtime JavaScript compatível
           -> ffmpeg quando necessário
           -> cookies / headers / tokens somente quando legitimamente configurados
```

O diagnóstico deve informar separadamente, quando tecnicamente possível:

- versão do mpv/libmpv;
- versão do yt-dlp;
- caminho efetivamente usado para yt-dlp;
- disponibilidade do FFmpeg;
- disponibilidade/capacidade do runtime JavaScript exigido pelo fluxo atual;
- disponibilidade/capacidade do EJS ou mecanismo oficial equivalente;
- falha específica de JavaScript challenge;
- falha específica de extractor;
- falha de autenticação/cookies;
- falha de rede;
- falha posterior no mpv/demuxer.

Não misturar tudo em uma única mensagem “yt-dlp desatualizado”.

## D. Runtime JavaScript e EJS

A documentação atual do yt-dlp para YouTube deve ser lida obrigatoriamente antes da implementação.

Se a versão atual exigir EJS e/ou runtime JavaScript para resolver desafios do YouTube:

- avaliar integração com a infraestrutura existente de `RuntimeComponents`;
- NÃO criar um segundo gerenciador de componentes;
- validar integridade/versionamento de qualquer componente gerenciado;
- não bloquear a UI durante bootstrap/update;
- não baixar/atualizar em cada reprodução;
- respeitar componentes explicitamente configurados pelo usuário;
- definir política de atualização coerente com os demais runtime components;
- registrar no diagnóstico o runtime realmente utilizado;
- adicionar testes de resolução e fallback;
- documentar claramente quando um componente é opcional ou obrigatório.

A decisão de empacotar, baixar ou apenas detectar um runtime JavaScript deve ser baseada na documentação oficial atual, segurança, licença, tamanho do pacote e arquitetura real do MPV.NET.

Não habilitar execução arbitrária de código remoto apenas para “fazer funcionar”.

## E. Remote components

Se a versão atual do yt-dlp oferecer mecanismos como remote components para EJS:

- estudar a documentação oficial atual;
- tratar como capacidade explícita, não como pressuposto;
- NÃO habilitar automaticamente download/execução remota sem análise de segurança;
- não modificar configuração do usuário silenciosamente;
- preferir uma solução determinística e auditável compatível com a política de runtime components do projeto;
- registrar claramente a origem e versão de qualquer componente baixado.

## F. PO Token / Proof of Origin

O YouTube pode exigir PO Token em determinados clientes, endpoints ou formatos.

O MPV.NET NÃO deve implementar gerador próprio de PO Token nesta tarefa.

Entretanto, o diagnóstico deve reconhecer, quando houver evidência, diferenças entre:

```text
yt-dlp desatualizado
extractor quebrado
desafio JavaScript não resolvido
runtime JavaScript ausente
EJS/componente equivalente ausente ou incompatível
PO Token necessário/ausente
cookies/autenticação necessários
restrição regional
conteúdo privado/removido
erro de rede
```

Se o yt-dlp oferecer configuração/plugin/provider oficial para PO Token, preservar a possibilidade de usuários avançados utilizarem esse mecanismo sem o MPV.NET capturar, registrar ou armazenar tokens indevidamente.

Nunca registrar PO Token, cookies, Authorization, tokens de sessão ou URLs assinadas completas em logs normais.

## G. Browser impersonation

Auditar a documentação atual do yt-dlp sobre browser impersonation.

Se determinado extractor depender de impersonation/TLS fingerprint ou dependência opcional como `curl_cffi`:

- não tornar isso obrigatório sem evidência;
- não instalar dependências pesadas automaticamente sem justificativa;
- reconhecer no diagnóstico quando a falha provavelmente está relacionada a capability opcional;
- documentar limitações;
- manter a arquitetura preparada para configuração avançada sem código específico por provedor.

## H. Headers, cookies e downloader options retornados pelo yt-dlp

Não implementar um resolvedor duplicado em C# apenas para capturar headers.

Auditar se o hook atual do mpv já propaga corretamente:

- HTTP headers;
- cookies;
- proxy;
- opções do downloader;
- fragmentos/chunks;
- URL final;
- subtitles/metadata relevantes.

Preferir o fluxo nativo do mpv + yt-dlp.

Somente adicionar lógica no MPV.NET quando existir lacuna comprovada.

## I. Critérios adicionais de aceite deste adendo

Esta iniciativa NÃO pode ser declarada concluída sem:

1. comportamento real de `--no-playlist` / `--yes-playlist` ou equivalente atual documentado e testado;
2. `watch?v=...&list=...` validado;
3. `index=` validado contra a posição real da playlist;
4. diagnóstico da cadeia mpv -> yt-dlp -> JS/EJS -> ffmpeg revisado;
5. requisitos atuais de EJS/runtime JavaScript documentados;
6. PO Token tratado como categoria de diagnóstico quando aplicável;
7. browser impersonation avaliado como capability opcional;
8. nenhum token/cookie/header sensível exposto;
9. nenhuma execução remota habilitada silenciosamente;
10. documentação técnica atualizada com essas decisões;
11. relatório final contendo versões e capacidades efetivamente validadas.


---

# ETAPA 4 — IPC E SEGUNDA INSTÂNCIA

Auditar o fluxo de encaminhamento para uma instância já aberta.

O projeto utiliza mensagem estruturada/JSON. Preservar essa arquitetura e garantir round-trip perfeito das URLs.

Testar obrigatoriamente:

- URL com `&`;
- URL com vários `=`;
- URL com `%`;
- URL Unicode;
- playlist do YouTube;
- múltiplas URLs;
- URL entre aspas recebida pelo Windows;
- modo `single`;
- modo `queue`;
- comandos existentes;
- ausência de truncamento ou double-decoding.

Garantir que uma URL enviada para a segunda instância produza o mesmo resultado de uma URL fornecida diretamente à primeira instância.

---

# ETAPA 5 — TESTES AUTOMATIZADOS DE ENTRADA E URL

Criar testes unitários/integração isolável para o novo pipeline.

Não depender da internet para a maioria desses testes.

Cobrir pelo menos:

## Normalização

- arquivo local absoluto;
- arquivo relativo;
- caminho UNC;
- URL HTTP;
- URL HTTPS;
- HLS;
- DASH;
- RTSP;
- RTMP;
- SRT;
- YouTube simples;
- youtu.be;
- parâmetros concatenados;
- playlist;
- Unicode;
- espaços;
- aspas externas;
- entradas inválidas.

## Command line

- URL simples;
- URL com `&`;
- URL com playlist;
- URL acompanhada de opções mpv;
- múltiplas mídias.

## IPC

- serialize/deserialize preserva byte semanticamente equivalente;
- parâmetros especiais não são perdidos;
- playlist e `index` são preservados.

## loadfile

Validar que `BuildLoadfileArgs` mantém a URL completa como um único argumento.

Não criar testes frágeis dependentes de detalhes internos sem necessidade.

---

# ETAPA 6 — YT-DLP: RESOLUÇÃO, VERSÃO, DIAGNÓSTICO E ATUALIZAÇÃO

O projeto já possui infraestrutura de componentes de runtime e resolução de `yt-dlp.exe`.

Aproveitar essa infraestrutura; não criar um segundo sistema paralelo.

Auditar:

- como `yt-dlp.exe` é localizado;
- cache de componentes;
- validação do executável;
- atualização;
- metadata/digest;
- refresh de `ytdl-path` após bootstrap;
- comportamento quando o executável ainda não está disponível no primeiro playback;
- comportamento quando existe uma versão explícita configurada pelo usuário.

Preservar a preferência do usuário quando `ytdl-path` estiver explicitamente configurado.

Adicionar, quando tecnicamente seguro:

- capacidade de identificar a versão instalada;
- diagnóstico legível da versão usada;
- detecção de componente ausente/inválido;
- mecanismo de atualização coerente com `RuntimeComponents`;
- possibilidade de atualização sob demanda ou no fluxo já existente de componentes;
- não bloquear a UI enquanto verifica/baixa componente.

Não executar atualização do executável a cada reprodução.

Não introduzir chamadas frequentes ao GitHub ou à internet sem cache/metadata adequados.

Se a reprodução falhar devido a erro típico de extractor, registrar informação suficiente para orientar diagnóstico sem expor dados sensíveis.

---

# ETAPA 7 — DIAGNÓSTICO DE ERROS DE STREAMING

Melhorar a classificação e o contexto de erros sem esconder a mensagem original do mpv/yt-dlp.

Quando houver evidência suficiente, distinguir categorias como:

- URL inválida;
- DNS/rede indisponível;
- timeout;
- HTTP 4xx/5xx;
- vídeo removido;
- vídeo privado;
- autenticação necessária;
- restrição regional;
- extractor do yt-dlp incompatível/desatualizado;
- stream encerrado;
- protocolo não suportado;
- certificado/TLS;
- falha no demuxer;
- falha na playlist.

Não afirmar uma causa sem evidência.

Separar:

```text
Categoria provável
Mensagem original
Componente relacionado
Ação sugerida (quando segura)
```

Não mostrar stack trace técnico ao usuário comum.

Manter detalhes completos nos logs de diagnóstico quando apropriado.

---

# ETAPA 8 — STREAMING AO VIVO E RECONEXÃO CONTROLADA

Auditar transmissões classificadas como:

- HLS live;
- DASH live;
- RTSP;
- RTMP/RTMPS;
- UDP;
- TCP;
- SRT/SRTP;
- outros streams contínuos suportados.

Implementar reconexão somente quando tecnicamente apropriado.

Requisitos:

- não criar loop infinito;
- limite claro de tentativas;
- atraso/backoff entre tentativas;
- cancelamento imediato se usuário trocar mídia ou fechar o aplicativo;
- não reiniciar arquivos locais;
- não reiniciar vídeo sob demanda concluído normalmente;
- diferenciar EOF normal de falha transitória;
- não criar duas reproduções concorrentes;
- preservar a UI responsiva;
- registrar tentativas nos logs.

Preferir recursos/configurações nativas do mpv antes de criar um mecanismo manual complexo.

Se o mpv já possui opções adequadas para reconexão de determinado protocolo, utilizar/configurar essas opções de forma centralizada.

---

# ETAPA 9 — POLÍTICA DE CACHE DE REDE

Auditar `NetworkCachePolicy` e todos os lugares onde opções automáticas de cache são aplicadas.

Objetivo:

aplicar cache adequado ao tipo real de mídia sem sobrescrever preferências explícitas do usuário.

Revisar os perfis existentes:

```text
off
low-latency
balanced
resilient
```

Revisar comportamento para:

- HTTP progressivo;
- HLS VOD;
- HLS live;
- DASH VOD;
- DASH live;
- FTP/SFTP;
- RTSP;
- RTMP;
- SRT;
- stream genérico;
- mídia resolvida por yt-dlp.

Evitar classificar automaticamente uma URL do YouTube apenas como HTTP progressivo se isso resultar em política incorreta depois que yt-dlp resolver HLS/DASH.

Investigar se o mpv expõe propriedades/eventos suficientes para conhecer o demuxer/stream final e, se necessário, ajustar a estratégia sem reiniciar o playback.

Regra obrigatória:

**opções explícitas do usuário em command line ou `mpv.conf` sempre prevalecem sobre políticas automáticas.**

Adicionar testes para resolução de política.

---

# ETAPA 10 — DESEMPENHO DO FLUXO DE REPRODUÇÃO

Fazer auditoria orientada por evidência, especialmente nos caminhos executados durante abertura/troca de mídia.

Investigar:

- chamadas repetidas a `GetProperty*` do mpv;
- parsing repetido do JSON de playlist;
- leituras repetidas de `mpv.conf`;
- acessos desnecessários a disco;
- chamadas síncronas em UI thread;
- tasks criadas desnecessariamente;
- polling desnecessário;
- locks muito amplos;
- alocações repetidas em caminhos quentes;
- manipulação duplicada de URL;
- consultas repetidas ao filesystem para a mesma entrada;
- verificações de runtime components em excesso.

Não otimizar baseado apenas em hipótese.

Para cada otimização relevante, registrar:

```text
Custo atual observado:
Causa:
Alteração:
Risco:
Como foi validado:
```

Preservar legibilidade. Não trocar código claro por micro-otimizações sem benefício mensurável ou evidente.

---

# ETAPA 11 — AUDITORIA ARQUITETURAL E QUALIDADE FINAL

Depois que TODAS as funcionalidades anteriores estiverem implementadas e estáveis, fazer uma revisão final do código alterado e das áreas diretamente relacionadas.

Validar:

## Arquitetura

- responsabilidades corretas;
- dependências coerentes;
- baixo acoplamento;
- alta coesão;
- nenhuma regra de domínio/reprodução indevidamente colocada em UI;
- nenhuma dependência circular nova;
- nenhuma abstração criada sem necessidade.

## Duplicidade

- normalização de mídia existe em um único lugar;
- classificação de URL não foi duplicada;
- regras de cache não foram copiadas para UI/player;
- tratamento de yt-dlp não foi duplicado;
- IPC não mantém parser paralelo diferente do fluxo principal.

## Recursos

- `IDisposable` corretamente utilizado;
- streams fechados;
- cancellation tokens respeitados;
- events desregistrados quando necessário;
- tasks observadas;
- nenhum handle/native resource novo com lifetime incorreto.

## Concorrência

- ausência de race evidente;
- fechamento durante download/update seguro;
- fechamento durante reconnect seguro;
- troca de mídia cancela trabalho da mídia anterior;
- não há deadlock entre UI e player.

## Erros

- exceções não são silenciosamente engolidas;
- logs possuem contexto;
- mensagens ao usuário são compreensíveis;
- falhas recuperáveis não encerram o app;
- falhas fatais não são mascaradas.

## Segurança e privacidade

- não registrar cookies;
- não registrar authorization headers;
- não registrar tokens de sessão;
- evitar logar query strings potencialmente sensíveis integralmente quando não necessário;
- não executar argumentos provenientes de URL como comandos;
- não passar dados de mídia para shell desnecessariamente.

## Nullability e APIs

- eliminar warnings novos;
- evitar `!` sem justificativa;
- remover código novo obsoleto;
- usar APIs modernas do .NET coerentes com o target atual;
- não alterar target framework nesta tarefa, a menos que o repositório já tenha uma migração em andamento explicitamente relacionada.

## Documentação

Atualizar documentação existente apenas quando houver comportamento novo consolidado.

Preferir:

- `docs/manual.md` para comportamento do usuário;
- `docs/developer/mpv-integration.md` para integração;
- `docs/developer/architecture.md` para arquitetura;
- `docs/guia-operacional.md` para operação/manutenção.

Evitar criar documentos redundantes.

---

# MATRIZ DE TESTES MANUAIS OBRIGATÓRIA

Além dos testes automatizados, ao final validar manualmente quando o ambiente permitir:

## Arquivos locais

- vídeo;
- áudio;
- legenda externa;
- playlist local;
- pasta com múltiplos arquivos.

## URLs

- HTTP direto;
- HLS;
- DASH;
- stream live disponível para teste;
- YouTube vídeo simples;
- youtu.be;
- YouTube com `t`;
- YouTube com `si`;
- YouTube com `list`;
- YouTube com `list` + `index`;
- URL recebida por clipboard;
- URL recebida por drag/drop;
- URL recebida via linha de comando;
- URL encaminhada à segunda instância.

## Playlist YouTube

Confirmar:

- fila populada;
- item atual correto;
- próximo funciona;
- anterior funciona;
- avanço automático funciona;
- título/metadados atualizam quando mpv/yt-dlp resolver;
- não há duplicação.

## UI/Windows

- SMTC continua funcionando;
- botões próximo/anterior refletem playlist;
- fullscreen;
- menu de contexto;
- OSC;
- tema claro/escuro quando elementos relacionados forem tocados;
- fechamento durante reprodução online;
- fechamento durante tentativa de reconnect;
- fechamento durante bootstrap/update de componente.

---

# CRITÉRIOS DE ACEITE GERAIS

A tarefa somente pode ser considerada concluída quando:

1. URLs do YouTube com parâmetros adicionais não são truncadas nem separadas incorretamente;
2. URLs `youtube.com` e `youtu.be` funcionam pelo mesmo pipeline;
3. playlist do YouTube preenche automaticamente a fila do mpv;
4. `index=` é respeitado quando tecnicamente fornecido pelo mpv/yt-dlp;
5. vídeo sem `list=` não cria playlist indevida;
6. entrada é normalizada centralmente;
7. command line, clipboard, drag/drop e IPC têm comportamento consistente;
8. segunda instância preserva integralmente as URLs;
9. `yt-dlp` continua respeitando configuração explícita do usuário;
10. resolução/diagnóstico do `yt-dlp` está mais robusto;
11. erros de streaming possuem contexto útil sem conclusões falsas;
12. lives podem se recuperar de falhas transitórias quando apropriado sem loop infinito;
13. cache continua respeitando opções explícitas do usuário;
14. não houve regressão na reprodução de arquivos locais;
15. não houve regressão em playlists locais;
16. não houve regressão no SMTC;
17. testes automatizados cobrem os principais formatos de URL;
18. build passa;
19. suíte de testes passa;
20. nenhum warning novo relevante é introduzido;
21. diff final não contém duplicação arquitetural evitável;
22. documentação relacionada está atualizada.

---

# ESTRATÉGIA DE COMMITS

Os commits devem refletir etapas pequenas.

Sugestões, adaptando aos arquivos efetivamente alterados:

```text
test(media): add online media input baseline cases
refactor(media): centralize media input normalization
fix(youtube): preserve URL query parameters
feat(youtube): support playlist queue expansion
fix(ipc): preserve online media URLs across instances
test(youtube): cover playlist and parameterized URLs
feat(runtime): improve yt-dlp diagnostics
feat(streaming): add controlled live stream recovery
refactor(streaming): refine network cache policy
perf(playback): reduce repeated media loading work
refactor(playback): finalize online media architecture
```

Não use exatamente esses commits se a alteração real não corresponder.

---

# NÃO FAZER

Não:

- implementar um cliente próprio do YouTube;
- chamar YouTube Data API apenas para reproduzir vídeos;
- baixar vídeos permanentemente como solução para playback;
- remover parâmetros desconhecidos de URL;
- converter playlist do YouTube manualmente em centenas de requests C# sem necessidade;
- substituir yt-dlp por parser próprio;
- alterar libmpv sem necessidade demonstrada;
- desativar validações para fazer um teste passar;
- esconder falhas com `catch { }` vazio;
- bloquear UI esperando rede;
- atualizar yt-dlp a cada reprodução;
- ignorar configuração explícita do usuário;
- quebrar compatibilidade com `mpv.conf`, `input.conf`, command line ou extensões;
- realizar refatoração global não relacionada;
- misturar grandes mudanças arquiteturais com correções pequenas no mesmo commit.

---

# ENTREGA FINAL DO CODEX

Ao terminar, apresentar relatório contendo:

```text
Resumo executivo

Problema original reproduzido

Causa raiz da URL do YouTube com parâmetros

Como o novo pipeline de entrada funciona

Como playlists do YouTube são tratadas

Como IPC/segunda instância preserva URLs

Melhorias realizadas no yt-dlp

Melhorias realizadas em streaming/reconexão

Melhorias realizadas no cache

Otimizações de desempenho realizadas

Arquivos alterados

Testes adicionados/alterados

Resultado de build

Resultado dos testes

Testes manuais executados

Riscos ou limitações restantes

Itens explicitamente não alterados

Commits criados

Confirmação de push
```

Não declarar uma funcionalidade como funcionando se não houver evidência por teste automatizado, teste manual ou comportamento documentado do mpv/yt-dlp.

Se alguma parte depender de comportamento externo do YouTube/yt-dlp e não puder ser validada offline, declarar explicitamente essa limitação no relatório.
