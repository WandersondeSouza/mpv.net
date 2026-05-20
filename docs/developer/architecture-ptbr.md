# Arquitetura do Projeto mpv.net

## Objetivo

Este documento ajuda mantenedores, desenvolvedores e agentes de IA a entender a arquitetura geral do fork mpv.net.

O foco é mapear responsabilidades reais do código atual, áreas críticas e fluxos que devem ser preservados.

---

# Visão geral

O mpv.net é um frontend Windows para o mpv/libmpv.

O projeto usa o motor do mpv para reprodução multimídia e implementa uma camada Windows com WinForms, WPF, comandos próprios, configuração compatível com mpv e integração com recursos do sistema.

Prioridades arquiteturais:

1. preservar compatibilidade com mpv/libmpv;
2. manter configuração baseada em arquivos simples;
3. preservar scripts, atalhos e comandos existentes;
4. isolar alterações de UI quando possível;
5. evitar refatorações amplas sem validação manual.

---

# Solução e camadas

```text
src/MpvNet.sln
  MpvNet/
  MpvNet.Windows/
  NGettext.Wpf/
  MpvNet.Extension/
```

## `MpvNet`

Núcleo da aplicação:

- integração com libmpv;
- comandos internos;
- linha de comando;
- resolução de configuração;
- `input.conf`;
- carregamento de extensões;
- estado do player.

## `MpvNet.Windows`

Frontend Windows:

- entry point;
- janela principal WinForms;
- janelas auxiliares WPF;
- comandos de UI;
- tema;
- hotkeys globais;
- associação de arquivos;
- integração com APIs nativas do Windows.

## `NGettext.Wpf`

Suporte de localização usado pelo frontend WPF.

## `MpvNet.Extension`

Exemplo de extensão .NET carregável.

---

# Fluxo de inicialização

Arquivo principal: `src/MpvNet.Windows/Program.cs`

Fluxo:

1. configura produto, tradução e handlers globais de exceção;
2. anexa console quando aplicável;
3. trata `--register-file-associations`;
4. chama `App.Init()`;
5. chama `Theme.Init()`;
6. cria `Mutex` baseado em `App.ConfPath`;
7. aplica regra de instância única, fila ou múltiplas instâncias;
8. processa comandos informativos de terminal;
9. inicia modo sem janela quando `--o=` está presente;
10. abre `WinForms.MainForm` no fluxo normal.

Arquivos relacionados:

- `src/MpvNet/App.cs`;
- `src/MpvNet/CommandLine.cs`;
- `src/MpvNet.Windows/WinForms/MainForm.cs`;
- `src/MpvNet.Windows/UI/Theme.cs`.

---

# Integração com mpv/libmpv

Arquivos principais:

- `src/MpvNet/Player.cs`;
- `src/MpvNet/MpvClient.cs`;
- `src/MpvNet/Native/LibMpv.cs`.

Responsabilidades:

- criar contexto `mpv_create`;
- registrar eventos;
- configurar propriedades iniciais;
- definir `config-dir`;
- carregar `input.conf` em memória;
- processar argumentos antes de `mpv_initialize`;
- chamar `mpv_initialize`;
- criar cliente `mpvnet`;
- iniciar loops de evento;
- observar propriedades como `pause`, `video-rotate` e `playlist-pos`;
- destruir handles no encerramento.

Essa camada é a área de maior risco do projeto.

---

# Interface gráfica

Arquivos principais:

- `src/MpvNet.Windows/WinForms/MainForm.cs`;
- `src/MpvNet.Windows/WPF/ConfWindow.xaml`;
- `src/MpvNet.Windows/WPF/InputWindow.xaml`;
- `src/MpvNet.Windows/WPF/Views/AboutWindow.xaml`;
- `src/MpvNet.Windows/WPF/Resources.xaml`;
- `src/MpvNet.Windows/UI/Theme.cs`;
- `src/MpvNet.Windows/UI/GlobalHotkey.cs`.

A janela principal é WinForms. Janelas auxiliares e editores usam WPF. Essa combinação exige cuidado com ownership de janela, DPI, fullscreen e integração com o handle nativo usado pelo libmpv.

---

# Sistema de configuração

Arquivos:

- `src/MpvNet/Player.cs`;
- `src/MpvNet/App.cs`;
- `src/MpvNet/Settings.cs`;
- `src/MpvNet/InputConf.cs`;
- `src/MpvNet.Windows/UI/Theme.cs`;
- `src/MpvNet.Windows/UI/GlobalHotkey.cs`.

Pasta de configuração:

1. `MPVNET_HOME`;
2. `portable_config`;
3. `%APPDATA%\mpv.net`.

Arquivos:

- `mpv.conf`;
- `mpvnet.conf`;
- `input.conf`;
- `settings.xml`;
- `theme.conf`;
- `global-input.conf`.

---

# Sistema de comandos

Arquivos:

- `src/MpvNet/Command.cs`;
- `src/MpvNet.Windows/GuiCommand.cs`;
- `src/MpvNet/InputHelp.cs`;
- `src/MpvNet/InputConf.cs`;
- `src/MpvNet/CommandLine.cs`.

O projeto aceita comandos mpv diretamente e adiciona comandos próprios, normalmente chamados por:

```text
script-message-to mpvnet <comando>
```

Comandos marcados como deprecated ainda podem ser usados por configurações antigas e não devem ser removidos sem migração.

---

# Scripts e extensões

Scripts Lua/JavaScript são responsabilidade do mpv e seguem a estrutura de configuração compatível:

- `scripts`;
- `script-opts`.

Extensões .NET são carregadas de:

```text
extensions
```

O carregamento é feito por `ExtensionLoader` após a janela principal informar que está carregada.

---

# Build e empacotamento

Arquivos:

- `src/MpvNet.Windows/MpvNet.Windows.csproj`;
- `src/Directory.Packages.props`;
- `src/Tools/release-mpv.net.ps1`;
- `src/Setup/Inno/inno-setup.iss`.

O executável é `mpvnet.exe`. O projeto da aplicação define `win-x64` como runtime padrão, e o script de release atual publica `win-x64`, copia DLLs nativas x64, cria ZIP portátil e gera instalador x64.

---

# Áreas consideradas sensíveis

## Integração com libmpv

Risco alto:

- reprodução;
- eventos;
- propriedades;
- threading;
- scripts;
- compatibilidade com mpv.

## Configuração

Risco alto:

- pasta de configuração;
- migrações automáticas;
- `input.conf`;
- `mpv.conf`;
- `mpvnet.conf`;
- modo portátil.

## UI/fullscreen

Risco médio/alto:

- DPI;
- fullscreen;
- múltiplos monitores;
- menu de contexto;
- foco;
- atalhos;
- tema claro/escuro.

## Release

Risco médio:

- caminhos fixos de ferramentas;
- dependências nativas;
- publicação Debug;
- ZIP portátil sem `portable_config`.

---

# Recomendações para agentes

1. Antes de alterar código, localizar a camada correta.
2. Preferir mudanças pequenas.
3. Preservar comandos, opções e arquivos existentes.
4. Atualizar documentação quando comportamento mudar.
5. Validar manualmente fullscreen, input e configuração quando tocar UI ou libmpv.
6. Separar documentação validada de hipóteses ainda pendentes.
