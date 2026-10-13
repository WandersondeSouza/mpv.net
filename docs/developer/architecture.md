# Arquitetura do MPV.NET Media Player

## Propósito

Esta página resume a estrutura do repositório, as responsabilidades dos principais projetos e o fluxo entre a interface do Windows e o mpv/libmpv. Detalhes de configuração, integração nativa, interface e release ficam nas páginas técnicas específicas.

## Visão geral

O MPV.NET Media Player é um aplicativo Windows baseado no mpv.net. O mpv/libmpv executa a reprodução; o código do fork integra esse motor à interface Windows, aos comandos, à configuração e aos recursos do sistema.

A separação principal é entre:

- **MpvNet**: núcleo da aplicação e coordenação do player;
- **MpvNet.Windows**: janela, comandos de interface e integrações Windows;
- **mpv/libmpv**: opções e comandos do motor, leitura de mídia e reprodução.

A compatibilidade com opções, comandos e arquivos de configuração do mpv orienta as decisões de arquitetura.

## Projetos principais

A solução principal está em `src/MpvNet.sln`. O repositório também contém um exemplo separado de extensão .NET.

| Projeto | Responsabilidade |
| --- | --- |
| `src/MpvNet` | Núcleo, estado do player, configuração, linha de comando, comandos e fluxo de mídia. |
| `src/MpvNet.Windows` | Aplicativo Windows, janela WinForms, janelas auxiliares WPF e integrações nativas do Windows. |
| `src/NGettext.Wpf` | Localização usada pelas janelas WPF. |
| `src/MpvNet.Tests` | Testes automatizados do núcleo e dos contratos arquiteturais. |
| `src/MpvNet.Pacote` | Empacotamento MSIX/WAP para distribuição pela Microsoft Store. |
| `src/MpvNet.Extension/ExampleExtension` | Exemplo de extensão .NET carregável pelo player. |

## Inicialização

No fluxo gráfico:

1. `Program.Main` prepara os diretórios locais, registra a resolução das dependências nativas e inicializa a aplicação e o tema.
2. O programa aplica a política de instância e seleciona o modo de execução.
3. No modo gráfico, `MainForm` cria a janela principal e inicializa `Player` com o HWND da janela.
4. `Player` inicializa libmpv, cria o cliente `mpvnet`, registra eventos e propriedades e inicia os loops de eventos.
5. A janela processa os argumentos e carrega a mídia solicitada.

O modo de saída sem janela, iniciado com `--o=`, inicializa o player diretamente, sem criar `MainForm`.

## Entrada de mídia

As solicitações recebidas pela linha de comando e pela interface convergem no fluxo de carregamento de `Player`. `MediaInputNormalizer` normaliza as entradas; playlists passam por `PlaylistFile.PrepareForPlayback`; em seguida, o player envia os comandos de carregamento ao mpv/libmpv.

O frontend coordena a solicitação e apresenta o estado de reprodução. A interpretação e a reprodução da mídia permanecem sob responsabilidade do mpv/libmpv.

## Componentes e pontos de entrada

| Componente | Responsabilidade e arquivos principais |
| --- | --- |
| Inicialização | `src/MpvNet.Windows/Program.cs` prepara o processo; `src/MpvNet/App.cs` carrega configuração e opções da aplicação. |
| Player | `src/MpvNet/Integration/Mpv/Player.cs` e `Player.*.cs` coordenam estado, inicialização, eventos, ciclo de vida e carregamento de mídia. |
| Cliente e API nativa | `src/MpvNet/Integration/Mpv/MpvClient.cs` encapsula o cliente libmpv; `src/MpvNet/Native/LibMpv.cs` declara a API nativa e suas estruturas. |
| Interface Windows | `src/MpvNet.Windows/WinForms/MainForm.cs` mantém o estado da janela; `src/MpvNet.Windows/WinForms/MainForm.*.cs` separa áreas como comandos, menu, drag-and-drop, cursor e tela cheia. |
| Configuração e comandos | `src/MpvNet/Configuration/` contém os contratos do núcleo; `src/MpvNet.Windows/Commands/GuiCommand.cs` atende aos comandos da interface e `src/MpvNet/Command.cs` aos comandos do player. |
| Scripts e extensões | Scripts Lua/JavaScript seguem o layout do mpv; `ExtensionService`, em `src/MpvNet/Services/ExtensionLoader.cs`, carrega extensões .NET da pasta `extensions`. |

## Configuração e compatibilidade

A pasta ativa segue esta ordem: `MPVNET_HOME`, quando aponta para uma pasta existente; `portable_config`, quando existe ao lado do executável; e, por fim, o diretório padrão fornecido por `AppPaths`. A configuração do mpv e as opções próprias do frontend continuam separadas, e os formatos existentes de configuração permanecem contratos de compatibilidade.

Comandos antigos e adaptadores públicos podem permanecer no código para preservar configurações, scripts e extensões existentes. Os arquivos, a ordem completa de resolução e os detalhes do modo portátil estão em [Configuração](configuration.md).

## Ciclo de vida e limites entre componentes

`Player` coordena os handles libmpv, os loops de eventos e as tarefas associadas à reprodução. `MpvClient` encapsula as chamadas ao cliente nativo. No encerramento, o player interrompe e aguarda o trabalho pendente antes de liberar os handles.

`MainForm` é responsável pelo ciclo de vida da janela e pelos recursos de UI e integração com o Windows. A integração com libmpv e o ciclo de vida nativo estão detalhados em [Integração com mpv/libmpv](mpv-integration.md); tela cheia, DPI, menu e eventos de janela estão em [Interface Windows](windows-ui.md).

## Documentação relacionada

- [Manual do usuário](../manual.md)
- [Guia operacional](../guia-operacional.md)
- [Configuração](configuration.md)
- [Integração com mpv/libmpv](mpv-integration.md)
- [Interface Windows](windows-ui.md)
- [Build e release](build-release.md)
- [Localização](localization.md)
