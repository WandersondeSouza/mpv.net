# COMPLEMENTO — YT-DLP MULTISSERVIÇO, SERVIÇOS REGIONAIS E LOCALIZAÇÃO — MPV.NET MEDIA PLAYER

> **IMPORTANTE:** este documento é um complemento obrigatório de:
>
> `.ai/prompts/online-media-youtube-streaming-modernization.md`
>
> Antes de executar qualquer alteração deste arquivo, leia e siga integralmente o prompt principal. Este complemento NÃO substitui nenhuma regra, etapa, teste, restrição arquitetural, estratégia de commit ou critério de aceite já definido no prompt principal.
>
> Trate os dois documentos como **uma única iniciativa para a próxima versão do MPV.NET Media Player**.

---

# OBJETIVO DESTE COMPLEMENTO

Expandir a modernização de mídia online do MPV.NET para que o `yt-dlp` seja tratado como um **resolvedor genérico de mídia online**, e não apenas como uma dependência utilizada para YouTube.

O MPV.NET deve continuar baseado em:

```text
Entrada de mídia
    ↓
Normalização central
    ↓
mpv/libmpv
    ↓
yt-dlp quando aplicável
    ↓
stream / vídeo / áudio / live / playlist / coleção
    ↓
playlist e estado nativos do mpv
    ↓
UI / OSC / SMTC / próximo / anterior
```

A implementação deve aproveitar ao máximo o comportamento já existente no `mpv` + `yt-dlp`, evitando escrever extractors próprios ou clientes específicos para serviços externos.

Objetivos principais:

1. manter YouTube como integração prioritária;
2. ampliar o suporte validado para serviços internacionais e regionais relevantes;
3. dar prioridade especial à experiência de usuários da China, Japão e Coreia;
4. integrar vídeos, playlists, coleções, séries e lives quando o extractor correspondente do `yt-dlp` oferecer esse comportamento;
5. permitir que novos serviços suportados futuramente pelo `yt-dlp` funcionem sem alteração obrigatória no código do MPV.NET;
6. não criar uma whitelist rígida de domínios como condição para reprodução;
7. aproveitar o pacote de idiomas atual do MPV.NET para mensagens de mídia online;
8. melhorar diagnóstico quando um serviço exige autenticação, sofre bloqueio regional, altera seu site ou quebra temporariamente o extractor;
9. manter compatibilidade completa com arquivos locais, URLs diretas, HLS, DASH, RTSP e demais protocolos já suportados pelo mpv;
10. impedir que este trabalho introduza dependência direta das APIs comerciais desses serviços quando o objetivo é apenas playback.

---

# PRINCÍPIO ARQUITETURAL PRINCIPAL

Não transformar o MPV.NET em um agregador que conhece internamente a API de cada serviço.

A arquitetura desejada é:

```text
MPV.NET conhece:
- entrada de mídia;
- normalização;
- origem da entrada;
- estado de playback;
- playlist do mpv;
- erros/diagnóstico;
- integração UI/SMTC;
- disponibilidade e versão do yt-dlp.

MPV.NET NÃO deve precisar conhecer:
- HTML interno do Bilibili;
- endpoints privados do Niconico;
- APIs internas do Naver;
- formatos privados do Douyin/Douyu;
- estruturas internas do Dailymotion;
- regras próprias de extração de cada site.

Essas responsabilidades permanecem no:
mpv + yt-dlp.
```

Se o `yt-dlp` já possui extractor adequado, prefira utilizá-lo.

Se o `yt-dlp` não suporta determinado recurso ou serviço de forma confiável, NÃO implementar scraping próprio dentro do MPV.NET apenas para contornar a limitação.

---

# VERIFICAÇÃO OBRIGATÓRIA ANTES DE IMPLEMENTAR

Como a lista de extractors do `yt-dlp` muda constantemente, antes de codificar:

1. consultar a documentação oficial atual do `yt-dlp`;
2. verificar `supportedsites.md` atual;
3. verificar documentação atual do mpv para `ytdl`, `ytdl-format`, `ytdl-raw-options`, playlists e `loadfile`;
4. confirmar quais tipos de URL de cada serviço continuam efetivamente suportados;
5. identificar se existem limitações conhecidas recentes;
6. não considerar suporte histórico como garantia de suporte atual;
7. registrar no relatório de implementação a versão do `yt-dlp` utilizada nos testes.

Não manter no código uma cópia completa da lista `supportedsites.md`.

Não criar atualização manual da lista de sites dentro do MPV.NET.

A capacidade genérica deve acompanhar a evolução do `yt-dlp`.

---

# SERVIÇOS PRIORITÁRIOS

A primeira versão desta expansão deve dar prioridade de validação aos serviços abaixo.

A ordem abaixo é uma prioridade de TESTE e QUALIDADE, não uma limitação artificial do player.

## Nível A — prioridade principal

### 1. YouTube / YouTube Music

Manter e aprofundar tudo que já está previsto no prompt principal:

- vídeo individual;
- `youtu.be`;
- parâmetros adicionais;
- playlist;
- `list=`;
- `index=`;
- mixes quando resolvidos corretamente pelo yt-dlp;
- live;
- conteúdo de áudio quando aplicável;
- metadados;
- próximo/anterior;
- playlist interna do mpv;
- integração SMTC.

O caso abaixo continua obrigatório:

```text
https://www.youtube.com/watch?v=0DLeH2FWFTE&list=RD0DLeH2FWFTE
```

Não remover `list` para forçar reprodução isolada.

Quando a intenção da URL resultar em playlist/mix, permitir que mpv/yt-dlp resolvam a coleção segundo o comportamento suportado.

### 2. Bilibili — prioridade especial para usuários chineses

Considerar Bilibili uma integração de primeira classe para validação do MPV.NET.

Investigar e testar, quando suportado pela versão atual do `yt-dlp`:

- vídeo individual;
- vídeo multiparte / anthology;
- playlist;
- coleção;
- favoritos;
- séries;
- Bangumi;
- conteúdo de áudio;
- live;
- URLs compartilhadas/encurtadas quando o extractor oficial do yt-dlp aceitar;
- metadados disponíveis;
- expansão para playlist interna do mpv quando houver múltiplas entradas.

Objetivo:

um usuário com interface `zh-CN` deve conseguir copiar uma URL válida do Bilibili e enviá-la ao MPV.NET pelo mesmo pipeline utilizado por YouTube.

Não criar API própria do Bilibili.

Não implementar scraping HTML próprio.

### 3. Niconico — prioridade para usuários japoneses

Quando suportado pelo extractor atual, validar:

- vídeo individual;
- playlist;
- série;
- canal;
- live;
- metadados;
- múltiplas entradas transformadas em playlist nativa do mpv.

Garantir que caracteres japoneses em URL, título e metadados não sejam corrompidos pelo pipeline de entrada, IPC ou logs.

### 4. Naver — prioridade para usuários coreanos

Quando suportado atualmente, validar:

- vídeo;
- live;
- playlist/coleção somente quando realmente suportada;
- metadados;
- Unicode coreano em título e URL.

Não simular playlist em C# quando o extractor retornar apenas um item.

### 5. Dailymotion — cobertura internacional/francófona

Validar:

- vídeo;
- playlist;
- usuário/canal quando o extractor atual resolver múltiplas entradas;
- live quando suportado;
- próximo/anterior via playlist nativa do mpv.

---

# Nível B — alta relevância complementar

Validar conforme suporte atual do `yt-dlp` e disponibilidade de testes reproduzíveis:

## China

- Douyin;
- Douyu;
- demais serviços chineses somente quando houver extractor atual e teste confiável.

Dar atenção especial a transmissões live do Douyu quando o suporte atual permitir.

## Global

- Twitch;
- Vimeo;
- SoundCloud;
- serviços adicionais que sejam amplamente utilizados e tenham extractor estável.

A implementação não deve ter código específico apenas porque esses nomes aparecem aqui. A lista define a matriz de testes, não a arquitetura.

---

# SERVIÇOS EXPERIMENTAIS / NÃO GARANTIDOS

Serviços cuja extração dependa de mudanças frequentes, autenticação complexa ou extractors atualmente instáveis devem ser tratados como compatibilidade dependente do `yt-dlp`.

Exemplos podem incluir, dependendo do estado atual no momento da implementação:

- iQIYI;
- Youku;
- outros serviços regionais com extractor parcial ou instável.

Antes de classificar qualquer serviço como experimental, verificar o estado atual do `yt-dlp`.

Não codificar permanentemente a classificação acima se a situação tiver mudado.

No relatório final, separar:

```text
Validado nesta versão
Suportado pelo yt-dlp, mas não validado manualmente
Requer autenticação/cookies
Possui limitação conhecida atual
Não suportado atualmente pelo extractor
```

Não declarar que o MPV.NET suporta plenamente um serviço somente porque o nome aparece em `supportedsites.md`.

---

# NÃO CRIAR WHITELIST RÍGIDA DE SITES

Não implementar algo equivalente a:

```csharp
if (host == "youtube.com" ||
    host == "bilibili.com" ||
    host == "nicovideo.jp")
{
    UseYtDlp();
}
```

como requisito para uma URL ser aceita.

URLs HTTP/HTTPS válidas devem continuar podendo chegar ao mpv.

O mpv deve poder reproduzir diretamente URLs/protocolos que ele entende e acionar seu hook yt-dlp quando necessário.

A lista de serviços prioritários deve existir para:

- testes;
- documentação;
- diagnóstico;
- telemetria local/logs técnicos quando existente e não invasiva;
- mensagens específicas somente quando tecnicamente justificadas.

Ela NÃO deve limitar novos extractors adicionados posteriormente ao `yt-dlp`.

---

# PLAYLIST, COLEÇÃO, SÉRIE E MÚLTIPLAS ENTRADAS

Generalizar o conceito previsto originalmente para playlist do YouTube.

Sempre que mpv/yt-dlp resolver uma URL em múltiplas entradas:

1. preferir a playlist interna do mpv;
2. não construir manualmente centenas de URLs no C#;
3. permitir próximo/anterior;
4. atualizar SMTC conforme o item atual mudar;
5. atualizar título/metadados;
6. avançar automaticamente ao final quando apropriado;
7. evitar duplicação de itens;
8. respeitar a posição/entrada indicada pela URL quando houver semântica equivalente;
9. não reiniciar desnecessariamente o item atual durante expansão;
10. cancelar corretamente quando o usuário trocar de mídia.

O código de UI NÃO deve precisar saber se a sequência surgiu de:

```text
YouTube playlist
Bilibili collection
Bilibili Bangumi
Niconico series
Dailymotion playlist
ou outro extractor futuro
```

Para a UI, tudo deve convergir para o conceito nativo de playlist/queue do player.

---

# VÍDEO ÚNICO VERSUS COLEÇÃO

Não transformar automaticamente todo vídeo proveniente de um site com capacidade de playlist em coleção.

Exemplos:

- URL de vídeo individual → um item;
- URL de playlist → múltiplos itens;
- URL de série/coleção → múltiplos itens quando o extractor assim resolver;
- URL de live → um stream contínuo;
- URL que contém contexto de coleção → respeitar o comportamento indicado por mpv/yt-dlp.

Não inferir coleção baseado apenas no domínio.

---

# LIVE STREAMING VIA YT-DLP

Integrar lives resolvidas pelo `yt-dlp` à política de streaming já prevista no prompt principal.

Após resolução, a mídia pode resultar em HLS, DASH ou outro stream.

Portanto:

- não aplicar cache apenas pelo domínio original;
- evitar tratar URL de Bilibili/Twitch/Douyu/YouTube Live como HTTP progressivo comum quando o stream final for live;
- avaliar propriedades finais expostas pelo mpv;
- preservar baixa latência quando apropriado;
- usar reconexão controlada somente conforme regras do prompt principal;
- não reiniciar live indefinidamente;
- cancelamento deve ser imediato ao trocar mídia ou fechar o app.

---

# AUTENTICAÇÃO, COOKIES E CONTEÚDO RESTRITO

Alguns serviços ou conteúdos podem exigir login/cookies.

Regras obrigatórias:

- NÃO coletar credenciais de usuário diretamente sem necessidade;
- NÃO criar tela de login própria para cada serviço nesta tarefa;
- NÃO armazenar senha de serviço no MPV.NET;
- NÃO registrar cookies em logs;
- NÃO registrar headers Authorization;
- NÃO registrar tokens de sessão;
- NÃO enviar cookies/credenciais para serviços diferentes do destino original;
- respeitar mecanismos de configuração já suportados por mpv/yt-dlp quando o usuário avançado explicitamente os configurar;
- nunca habilitar bypass de DRM;
- nunca implementar contorno de paywall ou acesso não autorizado.

Quando um conteúdo exigir autenticação, apresentar diagnóstico localizado e compreensível.

Exemplo conceitual:

```text
Este conteúdo exige autenticação no serviço de origem.
```

Não afirmar que credenciais resolverão o problema quando não houver evidência.

---

# DRM E CONTEÚDO NÃO REPRODUZÍVEL

Não implementar mecanismos para quebrar, remover ou contornar DRM.

Se o serviço entregar conteúdo protegido de forma não suportada pelo mpv/yt-dlp:

- falhar de forma segura;
- manter mensagem original técnica nos logs;
- apresentar ao usuário mensagem simples e localizada;
- não sugerir bypass.

---

# LOCALIZAÇÃO E PACOTE DE IDIOMAS

O MPV.NET possui um pacote multilíngue e essa funcionalidade deve ser integrada ao sistema de localização existente.

Antes de adicionar strings:

1. analisar a estrutura real da pasta `lang/`;
2. identificar todos os idiomas atualmente distribuídos pelo projeto;
3. seguir exatamente o mecanismo de fallback já adotado;
4. não duplicar chaves já existentes;
5. não introduzir textos de UI hardcoded em C#;
6. garantir UTF-8/Unicode correto;
7. manter variantes regionais existentes, como português, conforme arquitetura atual.

Dar atenção especial a:

- `zh-CN` para Bilibili/Douyin/Douyu;
- `ja` para Niconico;
- `ko` para Naver;
- `fr` para Dailymotion;
- todos os demais idiomas atualmente distribuídos pelo MPV.NET.

Adicionar/localizar apenas mensagens realmente necessárias à experiência.

Candidatas:

```text
Carregando mídia online…
Obtendo informações da mídia…
Obtendo playlist…
Carregando transmissão ao vivo…
Este conteúdo exige autenticação.
Este conteúdo não está disponível na sua região.
Este conteúdo não está mais disponível.
Não foi possível resolver esta URL.
O componente yt-dlp não está disponível.
O yt-dlp pode estar desatualizado.
Não foi possível carregar a playlist.
A transmissão foi encerrada.
Tentando reconectar à transmissão…
Formato de mídia não suportado pelo serviço de origem.
```

Antes de criar cada string, verificar se já existe equivalente funcional.

Não traduzir nomes de marcas como:

- YouTube;
- Bilibili;
- Niconico;
- Naver;
- Dailymotion;
- Twitch;
- Vimeo;
- yt-dlp.

---

# IDENTIFICAÇÃO DE SERVIÇO — SOMENTE QUANDO ÚTIL

Pode existir uma classificação leve do host/origem para melhorar diagnóstico e testes, por exemplo:

```text
OnlineMediaProvider
- Generic
- YouTube
- Bilibili
- Niconico
- Naver
- Dailymotion
- Twitch
- Vimeo
- Douyin
- Douyu
```

MAS só criar essa abstração se houver uso concreto.

Possíveis usos legítimos:

- enriquecer logs;
- escolher mensagem de diagnóstico;
- testes;
- documentação técnica;
- métricas estritamente locais se já houver infraestrutura compatível.

Ela NÃO pode decidir se uma URL é aceita ou não.

Evitar `switch` espalhado por toda aplicação.

Se criada, centralizar e manter extensível.

---

# PRIVACIDADE DOS LOGS

URLs de streaming podem conter:

- tokens;
- IDs de sessão;
- assinaturas temporárias;
- parâmetros privados;
- cookies embutidos;
- dados de autenticação.

Revisar `Log.SafeValue` e qualquer logging de URLs.

Nos logs normais, preferir registrar:

```text
Provider: Bilibili
Host: bilibili.com
Tipo: online media
Resultado: extractor failed
```

em vez de imprimir indiscriminadamente a query string completa.

Quando a URL completa for indispensável para diagnóstico avançado, utilizar somente mecanismo explicitamente seguro já existente e documentar o risco.

---

# TESTES AUTOMATIZADOS — MULTISSERVIÇO

A maioria dos testes NÃO deve depender da internet.

Criar testes parametrizados com URLs representativas para garantir que o pipeline:

- não trunca query string;
- não altera Unicode;
- não quebra `&`, `?`, `=`, `%`, `#`;
- não transforma URL em caminho local;
- preserva URL em IPC;
- preserva URL em command line;
- preserva URL em drag/drop/clipboard quando aplicável;
- envia a entrada como um único argumento para `loadfile`;
- não rejeita URL HTTP/HTTPS válida apenas por domínio desconhecido.

Adicionar casos representativos, usando identificadores fictícios quando não for necessário acesso real:

```text
YouTube
https://www.youtube.com/watch?v=VIDEO_ID&list=PLAYLIST_ID&index=3

Bilibili
https://www.bilibili.com/video/BV_TEST

Niconico
https://www.nicovideo.jp/watch/sm_TEST

Naver
https://tv.naver.com/v/TEST

Dailymotion
https://www.dailymotion.com/video/TEST

Twitch
https://www.twitch.tv/example

Vimeo
https://vimeo.com/123456789
```

Não acoplar testes unitários a detalhes temporários dos sites.

Testes unitários devem validar NOSSO pipeline, não o funcionamento da internet.

---

# TESTES DE INTEGRAÇÃO ONLINE — OPCIONAIS/CONTROLADOS

Separar testes online de testes determinísticos.

Quando o ambiente permitir e houver URLs públicas adequadas:

- executar manualmente ou em suíte marcada explicitamente como integração;
- não tornar build comum dependente da disponibilidade de Bilibili/YouTube/etc.;
- não usar conteúdo privado;
- não usar URLs que exijam conta pessoal;
- não deixar tokens/cookies no repositório;
- registrar versão do yt-dlp utilizada.

Para cada serviço Nível A, tentar validar pelo menos um exemplo público disponível no momento da execução.

Quando o serviço possuir múltiplas entradas, validar também um exemplo de playlist/coleção/série quando disponível e suportado.

---

# MATRIZ MANUAL OBRIGATÓRIA DE PROVEDORES

Ao final, quando tecnicamente possível, registrar:

```text
Provider | URL simples | Playlist/coleção | Live | Metadados | Próximo/Anterior | SMTC | Resultado
```

Incluir no mínimo:

- YouTube;
- Bilibili;
- Niconico;
- Naver;
- Dailymotion.

Adicionar, conforme disponibilidade:

- Douyin;
- Douyu;
- Twitch;
- Vimeo;
- SoundCloud.

Para serviços sem determinada capacidade, registrar `N/A` em vez de considerar falha.

---

# COMPORTAMENTO COM EXTRACTOR QUEBRADO OU DESATUALIZADO

Um serviço externo pode alterar seu site sem aviso.

O MPV.NET deve distinguir, quando houver evidência:

```text
Rede indisponível
URL inválida
Conteúdo removido
Conteúdo privado
Autenticação necessária
Bloqueio regional
Extractor não suportado
Extractor possivelmente desatualizado
Erro interno do yt-dlp
Falha do mpv após resolução
```

Não mascarar a mensagem original do mpv/yt-dlp nos logs.

A mensagem ao usuário deve ser curta e localizada.

Não afirmar automaticamente "atualize o yt-dlp" para todo erro.

Somente sugerir atualização quando o diagnóstico oferecer evidência razoável.

---

# ATUALIZAÇÃO DO YT-DLP E COMPATIBILIDADE FUTURA

Como este recurso passa a depender ainda mais da amplitude de extractors do `yt-dlp`, revisar o fluxo de atualização definido no prompt principal.

Objetivo:

- manter `yt-dlp` relativamente atual;
- não consultar atualização a cada playback;
- respeitar executável explicitamente configurado pelo usuário;
- não sobrescrever instalação externa sem autorização/arquitetura existente;
- não bloquear UI;
- validar binário antes de usá-lo;
- registrar versão em diagnóstico;
- permitir que novos extractors do yt-dlp funcionem automaticamente pelo pipeline genérico.

O sucesso arquitetural será demonstrado quando um novo serviço adicionado futuramente ao `yt-dlp` puder funcionar no MPV.NET sem necessidade de adicionar um novo `if (host == ...)`.

---

# UI / UX

Não criar uma tela cheia de logos de sites ou um seletor de provedor nesta tarefa.

O usuário deve continuar usando o comportamento natural do player:

```text
Copiar URL
    ↓
Abrir/colar/arrastar no MPV.NET
    ↓
Reprodução
```

A funcionalidade deve parecer parte natural do player.

Somente exibir informação de provedor quando agregar valor real, por exemplo em diagnóstico.

Preservar:

- OSC;
- SMTC;
- fullscreen;
- menus atuais;
- atalhos;
- playlist atual;
- histórico/recentes quando existente;
- segunda instância/IPC;
- drag/drop;
- clipboard;
- command line.

---

# SMTC E PLAYLIST MULTISSERVIÇO

Quando uma coleção/playlist for resolvida por qualquer extractor:

- `Next` deve avançar para o próximo item da playlist nativa;
- `Previous` deve voltar ao item anterior;
- título deve acompanhar o item corrente;
- artista/canal, quando fornecido adequadamente pelo mpv, pode ser refletido conforme arquitetura existente;
- capa/thumbnail somente deve ser utilizada se o fluxo atual do player já suportar isso de maneira segura;
- não baixar imagens permanentemente sem política definida;
- evitar chamadas adicionais ao site apenas para preencher SMTC se mpv já disponibilizar metadados.

O SMTC não deve conter lógica específica de Bilibili, YouTube ou Niconico.

---

# PERFORMANCE

Evitar que a expansão multissserviço provoque:

- execução duplicada do yt-dlp;
- resolução antecipada de URL em C# seguida por nova resolução no mpv;
- requisições de metadata duplicadas;
- parsing repetido da mesma URL em várias camadas;
- listas enormes materializadas desnecessariamente no frontend;
- bloqueio de UI durante resolução;
- polling excessivo de playlist;
- leitura repetida de configurações a cada evento.

Preferir eventos/propriedades nativas do mpv.

Medir antes de introduzir cache próprio.

---

# SEGURANÇA

Revisar especialmente:

- command injection;
- shell execution;
- argumentos recebidos de URL;
- escaping de `loadfile`;
- IPC;
- cookies;
- tokens;
- URLs assinadas;
- nomes de arquivo derivados de metadados;
- conteúdo externo usado em UI.

Uma URL ou metadata retornada pelo `yt-dlp` nunca deve se transformar automaticamente em comando de shell.

Não utilizar `Process.Start` para executar URLs de mídia como workaround de playback.

---

# DOCUMENTAÇÃO DO USUÁRIO

Atualizar `docs/manual.md` somente após o comportamento estar validado.

Explicar de forma prudente:

- MPV.NET pode reproduzir mídia online através do mpv + yt-dlp;
- compatibilidade depende dos extractors disponíveis na versão instalada do yt-dlp;
- sites podem alterar seus sistemas e temporariamente deixar de funcionar;
- alguns conteúdos podem exigir autenticação;
- conteúdos protegidos por DRM podem não ser reproduzíveis;
- disponibilidade pode variar por região.

Evitar promessas como:

```text
Suporta todos os sites da Internet.
```

ou

```text
Todos os sites listados pelo yt-dlp sempre funcionarão.
```

Preferir redação equivalente a:

```text
O MPV.NET utiliza mpv e yt-dlp para oferecer compatibilidade com diversos serviços de mídia online. A disponibilidade depende do serviço, do conteúdo e da versão do yt-dlp.
```

---

# DOCUMENTAÇÃO TÉCNICA

Atualizar `docs/developer/mpv-integration.md` com:

- fluxo genérico de mídia online;
- papel do mpv;
- papel do yt-dlp;
- onde ocorre normalização;
- como playlists/múltiplas entradas convergem para playlist nativa do mpv;
- política de fallback;
- diagnóstico;
- atualização do runtime component;
- regras de privacidade de logs.

Não criar documentação duplicada se o assunto já tiver seção adequada.

---

# CRITÉRIOS DE ACEITE ADICIONAIS

Além de TODOS os critérios do prompt principal, esta extensão só pode ser considerada concluída quando:

1. a arquitetura de mídia online não estiver limitada ao YouTube;
2. URLs HTTP/HTTPS de domínios desconhecidos continuarem elegíveis ao pipeline genérico;
3. Bilibili estiver explicitamente incluído na matriz de validação;
4. Niconico estiver explicitamente incluído na matriz de validação;
5. Naver estiver explicitamente incluído na matriz de validação;
6. Dailymotion estiver explicitamente incluído na matriz de validação;
7. YouTube continuar funcionando sem regressão;
8. múltiplas entradas retornadas pelo mpv/yt-dlp convergirem para a playlist nativa do player quando suportado;
9. SMTC próximo/anterior permanecer genérico e funcionar com playlists multissserviço;
10. Unicode chinês, japonês e coreano não for corrompido pelo pipeline;
11. IPC preservar URLs regionais integralmente;
12. não existir whitelist rígida impedindo novos serviços do yt-dlp;
13. não existir extractor próprio desnecessário no C#;
14. não existir cliente próprio de API dos provedores somente para reprodução;
15. mensagens novas estiverem integradas ao sistema real de localização;
16. `zh-CN`, `ja`, `ko`, `fr` e os demais idiomas distribuídos forem tratados conforme estratégia real do projeto;
17. nenhuma credencial/cookie/token for exposto em logs;
18. DRM não for contornado;
19. falha de um extractor externo não causar crash do aplicativo;
20. testes unitários permanecerem independentes da internet;
21. testes online forem claramente separados;
22. documentação não prometer compatibilidade permanente de serviços externos;
23. versão do yt-dlp utilizada na validação estiver registrada no relatório final;
24. build e suíte de testes existentes permanecerem aprovados;
25. não houver regressão em arquivos locais, playlists locais, protocolos diretos ou streaming já existente.

---

# COMMITS SUGERIDOS PARA ESTA EXTENSÃO

Adaptar aos arquivos realmente alterados.

Exemplos:

```text
test(media): add multi-provider online URL cases
refactor(media): keep yt-dlp provider resolution generic
feat(streaming): support multi-entry online media queues
feat(localization): add online media diagnostic strings
test(streaming): add regional provider integration matrix
docs(streaming): document multi-provider yt-dlp support
```

Não criar commit vazio apenas para seguir esta lista.

Manter commits pequenos e verificáveis conforme regra do prompt principal.

---

# NÃO FAZER — COMPLEMENTO

Além das proibições do prompt principal, NÃO:

- criar scraper do Bilibili;
- criar scraper do Niconico;
- criar scraper do Naver;
- criar scraper do Dailymotion;
- criar scraper do Douyin/Douyu;
- copiar extractors Python do yt-dlp para C#;
- implementar APIs reversas não documentadas;
- contornar DRM;
- contornar paywall;
- automatizar login de usuário;
- embutir cookies pessoais;
- versionar tokens de teste;
- manter lista completa de sites suportados hardcoded;
- rejeitar domínio novo apenas por não estar na nossa lista;
- duplicar resolução da URL em C# e novamente no mpv;
- usar web scraping para obter metadata que o mpv/yt-dlp já fornece;
- adicionar dependência pesada apenas para identificar hostname;
- mostrar mensagem específica de provedor sem evidência da causa;
- prometer suporte permanente a serviço externo.

---

# ENTREGA FINAL DO CODEX — CAMPOS ADICIONAIS

Além do relatório exigido no prompt principal, acrescentar:

```text
=== EXPANSÃO YT-DLP MULTISSERVIÇO ===

Versão do yt-dlp validada:
Versão do mpv/libmpv validada:

Arquitetura final do fluxo de mídia online:

Serviços Nível A testados:
- YouTube:
- Bilibili:
- Niconico:
- Naver:
- Dailymotion:

Serviços complementares testados:
- Douyin:
- Douyu:
- Twitch:
- Vimeo:
- SoundCloud:

Múltiplas entradas / playlists / coleções validadas:

Lives validadas:

Comportamento do SMTC com playlist online:

Idiomas/strings adicionados ou reutilizados:

Resultado específico para zh-CN:
Resultado específico para ja:
Resultado específico para ko:
Resultado específico para fr:

URLs/testes que dependem de internet:

Serviços suportados pelo yt-dlp mas não validados nesta execução:

Limitações conhecidas atuais:

Casos que exigem autenticação/cookies:

Proteções adotadas para logs/tokens/cookies:

Confirmação de que nenhum DRM/paywall foi contornado:

Confirmação de que não existe whitelist rígida de provedores:

Confirmação de que novos extractors do yt-dlp podem utilizar o pipeline genérico:
```

Não declarar `OK` quando o item não tiver sido realmente validado.

Usar `N/A`, `não testado` ou explicar a limitação quando apropriado.

---

# ORDEM DE EXECUÇÃO COM O PROMPT PRINCIPAL

Executar desta forma:

```text
1. Ler integralmente online-media-youtube-streaming-modernization.md
2. Ler integralmente este complemento
3. Fazer baseline do repositório
4. Corrigir e consolidar o pipeline genérico de entrada
5. Garantir YouTube + playlists
6. Generalizar a resolução para yt-dlp multissserviço
7. Validar Bilibili
8. Validar Niconico
9. Validar Naver
10. Validar Dailymotion
11. Validar serviços complementares disponíveis
12. Integrar diagnóstico + localização
13. Validar playlist/SMTC
14. Validar live/cache/reconexão
15. Executar testes completos de regressão
16. Atualizar documentação
17. Auditoria arquitetural final
18. Relatório final consolidado dos dois prompts
```

Não iniciar trabalho específico de um serviço antes de garantir que o pipeline genérico esteja correto.

O objetivo final não é implementar cinco players diferentes.

O objetivo final é possuir **um único MPV.NET Media Player com uma arquitetura genérica, sustentável e internacional de mídia online baseada em mpv + yt-dlp**.