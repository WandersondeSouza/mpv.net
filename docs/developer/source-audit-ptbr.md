# Auditoria Inicial do Código-Fonte

## Objetivo

Este documento registra a auditoria inicial do código-fonte do fork `WandersondeSouza/mpv.net`.

A finalidade é identificar estrutura real, projetos, pontos de entrada, dependências e áreas de manutenção antes de qualquer alteração funcional.

---

# Status da auditoria

A inspeção local da árvore do repositório confirmou a solução, os projetos principais, o ponto de entrada e os arquivos centrais de integração com o mpv/libmpv.

Esta auditoria substitui a etapa anterior, que dependia de busca remota e ainda não tinha localizado os arquivos reais do projeto.

---

# Estrutura principal localizada

| Área | Arquivo/pasta | Observação |
| --- | --- | --- |
| Solução | `src/MpvNet.sln` | Solução principal do projeto. |
| Biblioteca principal | `src/MpvNet/MpvNet.csproj` | Projeto `net10.0`, assembly `libmpvnet`. |
| Aplicação Windows | `src/MpvNet.Windows/MpvNet.Windows.csproj` | Projeto `WinExe`, `net10.0-windows7.0`, WPF + WinForms, assembly `mpvnet`. |
| Localização WPF | `src/NGettext.Wpf/NGettext.Wpf.csproj` | Projeto auxiliar de tradução/localização. |
| Extensão de exemplo | `src/MpvNet.Extension/ExampleExtension/ExampleExtension.csproj` | Exemplo de extensão .NET. |
| Versões NuGet | `src/Directory.Packages.props` | Gerenciamento centralizado de versões. |
| Propriedades comuns | `src/Directory.Build.props` | Propriedades MSBuild compartilhadas. |
| Release | `src/Tools/build-release-package.ps1` | Publicação x64, ZIP portátil, instalador Inno x64 e release GitHub. |
| Instalador | `src/Setup/Inno/build-windows-installer.iss` | Script do Inno Setup para instalador x64. |

---

# Ponto de entrada e inicialização

O ponto de entrada real está em `src/MpvNet.Windows/Program.cs`.

Fluxo resumido:

1. configura `RegistryHelp.ProductName` e `Translator.Current`;
2. inicializa WinForms com `Application.EnableVisualStyles()`;
3. registra handlers globais de exceção;
4. anexa console quando iniciado por terminal;
5. trata `--register-file-associations`;
6. chama `App.Init()`;
7. chama `Theme.Init()`;
8. cria `Mutex` baseado em `App.ConfPath` para controle de instância;
9. redireciona argumentos para a instância existente quando `process-instance` é `single` ou `queue`;
10. processa comandos de terminal como `--profile=help`, `--vd=help`, `--audio-device=help`, `--input-keylist` e `--version`;
11. inicia modo sem janela quando `--o=` está presente;
12. caso contrário, abre `WinForms.MainForm`.

Arquivos relacionados:

- `src/MpvNet.Windows/Program.cs`
- `src/MpvNet/App.cs`
- `src/MpvNet.Windows/WinForms/MainForm.cs`
- `src/MpvNet.Windows/UI/Theme.cs`
- `src/MpvNet/CommandLine.cs`

---

# Integração com mpv/libmpv

A integração central fica em:

- `src/MpvNet/Player.cs`
- `src/MpvNet/MpvClient.cs`
- `src/MpvNet/Native/LibMpv.cs`

Responsabilidades observadas:

- criação do contexto com `mpv_create`;
- registro de eventos com `mpv_request_event`;
- configuração inicial de propriedades mpv;
- definição de `config-dir`;
- carregamento de `input.conf` em memória;
- aplicação de argumentos de linha de comando antes e depois de `mpv_initialize`;
- criação do cliente secundário `mpvnet` com `mpv_create_client`;
- loop de eventos com `mpv_wait_event`;
- leitura e escrita de propriedades;
- envio de comandos por string e por vetor de argumentos.

---

# Configuração

A resolução da pasta de configuração está em `Player.ConfigFolder`.

Ordem real:

1. `MPVNET_HOME`, quando aponta para diretório existente;
2. `portable_config` ao lado do executável;
3. `%APPDATA%\mpv.net`, criado automaticamente quando necessário.

Arquivos principais:

- `mpv.conf`: `Player.ConfPath`;
- `mpvnet.conf`: `App.ConfPath`;
- `input.conf`: `App.InputConf`;
- `settings.xml`: `SettingsManager.SettingsFile`;
- `theme.conf`: carregado por `Theme.Init()`;
- `global-input.conf`: carregado por `GlobalHotkey`;
- `extensions`: carregada por `ExtensionLoader`.

---

# Comandos e input

Arquivos principais:

- `src/MpvNet/Command.cs`: comandos internos independentes da UI Windows;
- `src/MpvNet.Windows/GuiCommand.cs`: comandos de UI, diálogos, Windows e integração visual;
- `src/MpvNet/InputConf.cs`: leitura, combinação e migração do `input.conf`;
- `src/MpvNet/InputHelp.cs`: atalhos/menu padrão;
- `src/MpvNet/CommandLine.cs`: argumentos de linha de comando.

O projeto preserva compatibilidade com comandos do mpv e adiciona comandos próprios acionados por `script-message-to mpvnet`.

---

# Interface gráfica

A UI combina WinForms e WPF:

- `src/MpvNet.Windows/WinForms/MainForm.cs`: janela principal e integração com o handle usado pelo mpv;
- `src/MpvNet.Windows/WPF/ConfWindow.xaml`: editor de configuração;
- `src/MpvNet.Windows/WPF/InputWindow.xaml`: editor de atalhos;
- `src/MpvNet.Windows/WPF/Views/AboutWindow.xaml`: janela sobre;
- `src/MpvNet.Windows/UI/Theme.cs`: carregamento de tema;
- `src/MpvNet.Windows/UI/GlobalHotkey.cs`: atalhos globais.

Mudanças nessa área devem validar fullscreen, DPI, menu de contexto, atalhos, tema claro/escuro e múltiplos monitores.

---

# Dependências identificadas

Dependências NuGet centralizadas:

- `CommunityToolkit.Mvvm` `8.4.2`;
- `NGettext` `0.6.7`;
- `Microsoft.Xaml.Behaviors.Wpf` `1.1.142`.

Dependências nativas/externas observadas:

- `libmpv-2.dll`;
- `MediaInfo.dll`;
- 7-Zip, usado pelo script de release;
- Inno Setup, usado para gerar instalador;
- GitHub CLI, usado para criar release.

---

# Riscos técnicos por módulo

## Integração com mpv/libmpv

Alto risco. Alterações podem afetar reprodução, eventos, scripts, propriedades, compatibilidade com o mpv e estabilidade de threading.

## Configuração

Alto risco. Alterações em `ConfigFolder`, `mpv.conf`, `mpvnet.conf`, `input.conf` ou migrações automáticas podem quebrar instalações existentes.

## Comandos/input

Alto risco para compatibilidade. Scripts e atalhos podem depender de comandos antigos, inclusive comandos marcados como deprecated.

## UI/fullscreen

Risco médio/alto. A janela principal mistura WinForms, WPF e handle nativo usado pelo libmpv.

## Build/release

Risco médio. O script atual publica em Debug e possui caminhos fixos para ferramentas externas.

---

# Próximas validações recomendadas

1. Rodar build local da solução.
2. Confirmar execução com `libmpv-2.dll` e `MediaInfo.dll` disponíveis.
3. Validar abertura de arquivo, URL e múltiplos arquivos.
4. Validar modo single instance, queue e `--process-instance=multi`.
5. Validar pasta de configuração normal, portátil e `MPVNET_HOME`.
6. Validar release ZIP/instalador antes de documentar como fluxo definitivo.
