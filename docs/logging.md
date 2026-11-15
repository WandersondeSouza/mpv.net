# Logs de diagnóstico

A compilação com `EnableFileLogging=true` grava `Error` e
`Debug` em arquivo. Use essa opção para gerar uma build de suporte.

Falhas reconhecidas de transporte curl e de decodificação de vídeo durante
streaming também geram `Streaming runtime failure` em `Error` na build de
diagnóstico, mesmo com `debug-mode=no` e sem um evento final de erro. O resumo é limitado a uma entrada
por categoria e item da playlist; os detalhes repetidos ficam no diagnóstico.
O registro não reinicia a mídia nem troca a playlist. Erros de decodificação
não são classificados como falhas transitórias de rede.
O diagnóstico reconhece as variantes curl `Failure when sending/receiving`
e `Failed sending/receiving data ... the peer`. A quebra de linha final da
mensagem do mpv é removida para manter mensagem, ação e caminho juntos no resumo.

## Estado padrão

Todos os logs em arquivo do frontend ficam desabilitados por padrão, inclusive
erros e o arquivo de Trace `MpvNet-debug.log`. O logger não cria a pasta `Logs`
nem executa a retenção nessa build. Arquivos antigos são preservados.
O parâmetro nativo `--log-file` do mpv, quando solicitado explicitamente pelo
usuário, continua funcionando de forma independente.

O cancelamento de um download de componente pelo encerramento do aplicativo é
esperado e aparece somente em `Debug`. Timeout e falhas reais de download
continuam em `Error` nas builds de diagnóstico; o timeout informa o limite e os bytes recebidos.
Downloads retomáveis preservam parciais no fechamento, em timeout ou falha
transitória. O limite é de duas horas por transferência, com detecção de
60 segundos sem progresso. Em builds de diagnóstico, o bootstrap registra
bytes retomados, progresso a cada cinco segundos, velocidade média, duração
das fases e reaproveitamento de uma release com SHA-256 inalterado.

Os testes xUnit isolam a raiz de dados quando não há um override explícito.
Veja [Testes automatizados](guia-operacional.md#testes-automatizados).

Para gerar uma saída Release com o comportamento padrão do logger, use:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj -c Release -r win-x64 /p:EnableFileLogging=false
```

Os scripts de release usam o mesmo padrão.

## Como habilitar

Para gerar uma versão de diagnóstico com logs detalhados em arquivo:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj -c Release -r win-x64 /p:EnableFileLogging=true
```

Essa compilação também inicia com `debug-mode=yes`, salvo quando
`mpvnet.conf` define explicitamente `debug-mode=no`. Isso ativa os detalhes de
cache de rede, mudanças de pausa, mensagens `WARN`/`ERROR`/`FATAL` do libmpv e
o arquivo de `Trace` `MpvNet-debug.log` na pasta de configuração. Esse arquivo
de `Trace` é recriado na inicialização; os logs diários `mpvnet-YYYY-MM-DD.log`
continuam separados.

Nessas builds, `debug-mode=yes` também habilita `curl=trace`, `demux=debug`,
`lavf=debug` e `ffmpeg/demuxer=debug`. Uma configuração explícita de `msg-level`
no `mpv.conf` ou na linha de comando tem precedência. Os eventos selecionados
vão para o log diário sanitizado, incluindo status HTTP, ranges e tentativas de
transferência; o trace por quadro/pacote dos demais módulos não é habilitado.
Cabeçalhos sem interesse para transporte, credenciais e queries são ocultados.
URLs que trazem assinatura, token ou endereço do cliente em segmentos do
caminho (como `/signature/`, `/lsig/` e `/ip/`) têm o caminho ocultado antes
do limite de tamanho. Isso também vale para o alvo relativo de uma requisição
HTTP no trace do curl.
A saída do yt-dlp é decodificada quando recebida como string JSON e gravada
por linha; mensagens longas são divididas após a sanitização, preservando o
restante do diagnóstico. Dumps de argumentos de configuração são ocultados
por poderem conter senhas, cookies ou tokens.

Use `-EnableFileLogging` nos scripts de empacotamento do fork. No workflow
manual [release-packages.yml](../.github/workflows/release-packages.yml), use
`enable_file_logging=true` e `create_release=false` para gerar um
artefato de suporte.

## Gerar ZIP e instalador para teste

Execute estes comandos na raiz do repositório para gerar artefatos locais com
logs habilitados.

### Gerar apenas o ZIP portátil com logs

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-portable-zip.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release -EnableFileLogging
```

Saída de uma versão estável

```text
artifacts\release\MPV.NET-Media-Player-v<versao>-diagnostic-portable-x64\
artifacts\release\MPV.NET-Media-Player-v<versao>-diagnostic-portable-x64.zip
```

Para testar, extraia o ZIP e execute `mpvnet.exe` dentro da pasta extraída.

### Gerar apenas o instalador executável com logs

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-installer-exe.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release -EnableFileLogging
```

Saída de uma versão estável

```text
artifacts\release\MPV.NET-Media-Player-v<versao>-diagnostic-portable-x64\
artifacts\release\MPV.NET-Media-Player-v<versao>-diagnostic-setup-x64.exe
```

Para testar, execute o instalador gerado, abra o aplicativo instalado e
reproduza os cenários que deseja diagnosticar.

### Gerar ZIP e instalador juntos com logs

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease -EnableFileLogging
```

Esse comando gera os artefatos locais sem publicar no GitHub.

Após reproduzir o cenário, abra a pasta de logs de uma instalação padrão.

```powershell
explorer "$env:LOCALAPPDATA\mpv.net\Logs"
```

O arquivo do dia deve seguir o formato `mpvnet-YYYY-MM-DD.log`.

## Onde os logs ficam

Quando existe escrita em arquivo, o aplicativo grava em:

```text
%LOCALAPPDATA%\mpv.net\Logs
```

O pacote da Microsoft Store usa a raiz privada sob `LocalCache\Local`.
Com `MPVNET_LOCAL_APPDATA`, a raiz segue o valor informado. Execute
`mpvnet.exe --diagnose-paths` para verificar o caminho efetivo.

A pasta de logs compartilha a raiz de dados com o cache temporário do mpv.

```text
%LOCALAPPDATA%\mpv.net\Cache
```

Na inicialização, falhas ao limpar arquivos antigos em
`%LOCALAPPDATA%\mpv.net\Cache` ou `%LOCALAPPDATA%\mpv.net\Temp` também entram
nesse log somente em builds de diagnóstico, junto com os logs `Debug`.
Essas falhas não bloqueiam a abertura do player.

O nome do arquivo inclui a data local.

```text
mpvnet-YYYY-MM-DD.log
```

Exemplo:

```text
mpvnet-2026-06-02.log
```

Cada entrada inclui data, hora, nível e mensagem. Ao registrar uma exceção,
o logger inclui a pilha de chamadas e a exceção interna, quando disponível.

<a name="diagnostico-de-abertura-por-parametro"></a>

## Diagnóstico de abertura por parâmetro

Builds com logs habilitados registram detalhes adicionais do fluxo de abertura:

- argumentos recebidos pela linha de comando;
- opções aplicadas antes e depois de `mpv_initialize`;
- arquivos, playlists ou URLs classificados como entrada de mídia;
- comandos `loadfile` e `loadlist` enviados ao mpv;
- valor final de `idle` e se o frontend deve sair ao fim da reprodução;
- eventos `start-file`, `file-loaded`, `end-file` e `shutdown` emitidos pelo mpv.

URLs com usuário, senha, query string ou fragmento são mascaradas no log para
evitar expor credenciais e tokens de playlists privadas. Diagnósticos de cache
registram o tipo de mídia e a política automática, nunca a query original.

Com `debug-mode=yes` em uma build com logging de diagnóstico, `Network cache
state` registra política, tipo identificado, evidência VOD/live e opções efetivas
em `on_load`, `file-loaded`, mudanças de buffering/adaptação e `end-file`.
A contagem/duração total por sessão exclui startup, pausa e seek; o estado é
limpo por item. `capacityEstimate` distingue margem estimada adequada,
insuficiente, marginal ou desconhecida, sem prometer capacidade acima do bitrate.
`Network cache buffering` registra posição, duração aproximada, percentual e
taxa de entrada; `Network cache stream durations` aceita somente durações
numéricas finitas e não negativas de áudio/vídeo em `ts-per-stream`.
Esses valores descrevem o demuxer principal e não garantem cobertura de demuxers
externos. Ausência de métricas suficientes impede adaptação. Não há log por
frame/pacote nem emissão de métricas a cada amostra; somente as transições.

A leitura limitada de metadados do resolvedor serve à classificação e não
registra o JSON/URLs assinadas. `ExpiredUrl` e `Manifest` distinguem expiração
explícita e manifesto inválido; são erros definitivos, sem reconexão automática.
A política não diagnostica erro VP9/AV1 como falta de cache e não cria watchdog.

Somente com `EnableFileLogging=true` e `debug-mode=yes`, o app acrescenta `verbose=` a
`ytdl-raw-options`. Assim, o `yt-dlp` selecionado pelo hook do mpv grava a saída
de `stderr` no log diário depois da extração, inclusive quando ela falha.
Mensagens passam por remoção de URLs assinadas e valores de segredo. O JSON de
`stdout` não é registrado porque contém URLs temporárias dos fluxos. Em uma
build padrão, o app não acrescenta `verbose=` e não coleta a saída de diagnóstico
do yt-dlp, mesmo com `debug-mode=yes`; opções explícitas do usuário são preservadas. Para
reproduzir esse modo manualmente, o yt-dlp documenta `--verbose` como a opção
de informações de depuração e o mpv encaminha `ytdl-raw-options` ao processo
filho.

<a name="retencao"></a>

## Retenção

Ao inicializar uma build de diagnóstico, o logger remove arquivos
`mpvnet-YYYY-MM-DD.log` cuja data no nome seja anterior a três dias atrás.
A limpeza de `Cache` e `Temp` usa outro critério: a data da última
alteração do arquivo ou do diretório vazio.

A limpeza atua apenas dentro da pasta de logs e apenas em arquivos que seguem o
padrão `mpvnet-*.log`. Falhas de limpeza ou escrita são tratadas internamente e
não devem interromper a interface nem o player.

## Cuidados

Revise os logs antes de compartilhar. Remova senhas, tokens, nomes de usuário,
URLs privadas e dados pessoais. Se a URL for necessária ao diagnóstico,
mascare os trechos sensíveis.
