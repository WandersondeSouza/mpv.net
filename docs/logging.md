# Logs de diagnóstico

A edição padrão grava o log nativo do mpv quando você passa `--log-file`.
Para registrar também os eventos do aplicativo, use uma compilação de
diagnóstico.

## Coletar um log

1. Para coletar eventos do aplicativo, use um
   [pacote de diagnóstico](#gerar-zip-e-instalador-para-teste) ou
   [compile o executável](#como-habilitar).
2. Reproduza o problema e anote a hora aproximada e a versão do player.
3. Encontre o log daquele dia e, se existir, o `MpvNet-debug.log` em
   [Onde os logs ficam](#onde-os-logs-ficam).
4. [Revise os dados sensíveis](#cuidados) antes de compartilhar os arquivos.

Execute o comando abaixo na pasta do player para coletar somente o log nativo.

```powershell
$logNativo = Join-Path $env:TEMP "mpvnet-nativo-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
.\mpvnet.exe --process-instance=multi "--log-file=$logNativo"
```

Reproduza o problema nessa instância. O arquivo `mpvnet-nativo-*.log`
fica em `%TEMP%` e contém as mensagens do motor mpv.

## Onde os logs ficam

| Arquivo | Local |
| --- | --- |
| `mpvnet-YYYY-MM-DD.log` | Pasta de logs em `%LOCALAPPDATA%\mpv.net\Logs` em instalações comuns. |
| `MpvNet-debug.log` | Pasta ativa de configuração. A compilação de diagnóstico recria o arquivo quando `debug-mode=yes`. |

O pacote da Microsoft Store usa a raiz privada sob `LocalCache\Local`.
Quando `MPVNET_LOCAL_APPDATA` está definido, os dados usam a raiz indicada.
Execute `mpvnet.exe --diagnose-paths` para conferir as pastas de logs e
configuração.

O log diário registra data local, hora, nível e mensagem. Em caso de exceção,
inclui a pilha de chamadas e a exceção interna quando disponível. Falhas de
limpeza em `Cache` e `Temp` aparecem somente nas compilações com log em
arquivo e são tratadas durante a abertura do player.

## Estado padrão

Todos os logs em arquivo do frontend ficam desabilitados por padrão, inclusive
erros e o arquivo de Trace `MpvNet-debug.log`. O logger não cria a pasta `Logs`
nem executa a retenção nessa build. Arquivos antigos são preservados.
O parâmetro nativo `--log-file` do mpv, quando solicitado explicitamente pelo
usuário, continua funcionando de forma independente.

Use `/p:EnableFileLogging=false` para gerar uma saída com esse comportamento.

## Como habilitar

Compile o executável de diagnóstico na raiz do repositório.

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj -c Release -r win-x64 /p:EnableFileLogging=true
```

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

### Gerar apenas o instalador executável com logs

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\generate-installer-exe.ps1 -SourceDir .\src -OutputRootDir .\artifacts\release -EnableFileLogging
```

Saída de uma versão estável

```text
artifacts\release\MPV.NET-Media-Player-v<versao>-diagnostic-portable-x64\
artifacts\release\MPV.NET-Media-Player-v<versao>-diagnostic-setup-x64.exe
```

### Gerar ZIP e instalador juntos com logs

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Tools\build-release-package.ps1 .\src .\artifacts\release -SkipGitHubRelease -EnableFileLogging
```

<a name="diagnostico-de-abertura-por-parametro"></a>

## Diagnóstico de abertura por parâmetro

Os logs de diagnóstico registram abertura de mídia, cache e falhas de
transporte. A configuração `debug-mode=yes` habilita o detalhe adicional.

<details>
<summary>Eventos, níveis de log e sanitização</summary>

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

Builds com logs habilitados registram detalhes do fluxo de abertura:

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

Falhas reconhecidas de transporte curl e de decodificação de vídeo durante
streaming também geram `Streaming runtime failure` em `Error` na build de
diagnóstico, mesmo com `debug-mode=no` e sem um evento final de erro. O resumo é limitado a uma entrada
por categoria e item da playlist; os detalhes repetidos ficam no diagnóstico.
O registro não reinicia a mídia nem troca a playlist. Erros de decodificação
não são classificados como falhas transitórias de rede.
O diagnóstico reconhece as variantes curl `Failure when sending/receiving`
e `Failed sending/receiving data ... the peer`. A quebra de linha final da
mensagem do mpv é removida para manter mensagem, ação e caminho juntos no resumo.

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

</details>

<a name="retencao"></a>

## Retenção

Ao inicializar uma build de diagnóstico, o logger remove arquivos
`mpvnet-YYYY-MM-DD.log` cuja data no nome seja anterior a três dias atrás.
A limpeza de `Cache` e `Temp` usa outro critério: a data da última
alteração do arquivo ou do diretório vazio.

O logger limita essa retenção aos arquivos diários da pasta de logs e trata
falhas de limpeza e escrita internamente.

## Cuidados

Revise os logs antes de compartilhar. Remova senhas, tokens, nomes de usuário,
URLs privadas e dados pessoais. A sanitização do aplicativo se aplica aos
logs do frontend. Revise também o arquivo nativo criado com `--log-file`.
