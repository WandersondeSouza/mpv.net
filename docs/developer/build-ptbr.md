# Guia de Build e Ambiente de Desenvolvimento

## Objetivo

Este documento orienta como preparar o ambiente para estudar, compilar e manter o fork **mpv.net**.

> Status: estrutura real do projeto mapeada. Os comandos abaixo refletem os arquivos atuais, mas o build/release completo ainda deve ser validado em uma máquina Windows com as dependências nativas e ferramentas externas instaladas.

---

# Requisitos principais

Para desenvolvimento:

- Windows 10 ou Windows 11;
- SDK do .NET compatível com `net10.0` e `net10.0-windows7.0`;
- Visual Studio com workload de desenvolvimento desktop .NET;
- Git;
- acesso ao repositório no GitHub.

Para execução:

- Windows;
- .NET Desktop Runtime 10.0 quando o publish for framework-dependent;
- `libmpv-2.dll`;
- `MediaInfo.dll`;
- arquivos de `Locale`, quando aplicável.

Para release:

- 7-Zip em `C:\Program Files\7-Zip\7z.exe`;
- Inno Setup 6 em `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`;
- GitHub CLI (`gh`);
- variável `GH_TOKEN` configurada para criação de release.

---

# Solução e projetos

Solução principal:

```text
src/MpvNet.sln
```

Projetos principais:

| Projeto | Tipo | Target | Saída |
| --- | --- | --- | --- |
| `src/MpvNet/MpvNet.csproj` | biblioteca | `net10.0` | `libmpvnet` |
| `src/MpvNet.Windows/MpvNet.Windows.csproj` | aplicação Windows | `net10.0-windows7.0` | `mpvnet.exe` |
| `src/NGettext.Wpf/NGettext.Wpf.csproj` | biblioteca | legado/packages.config | suporte WPF/NGettext |
| `src/MpvNet.Extension/ExampleExtension/ExampleExtension.csproj` | exemplo | extensão .NET | exemplo carregável |

Pacotes NuGet versionados em `src/Directory.Packages.props`:

- `CommunityToolkit.Mvvm` `8.4.2`;
- `NGettext` `0.6.7`;
- `Microsoft.Xaml.Behaviors.Wpf` `1.1.142`.

---

# Clonando o fork

```powershell
git clone https://github.com/WandersondeSouza/mpv.net.git
cd mpv.net
```

---

# Abrindo no Visual Studio

1. Abra o Visual Studio.
2. Selecione **Open a project or solution**.
3. Abra `src/MpvNet.sln`.
4. Restaure os pacotes NuGet.
5. Compile em Debug.
6. Garanta que `libmpv-2.dll` e `MediaInfo.dll` estejam no diretório esperado de saída antes de executar.

---

# Build via terminal

Na raiz do repositório:

```powershell
dotnet restore src\MpvNet.sln
dotnet build src\MpvNet.sln
```

Para compilar apenas a aplicação Windows:

```powershell
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj
```

Para publicar como o script de release atual faz:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj --self-contained false --configuration Debug --runtime win-x64
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj --self-contained false --configuration Debug --runtime win-arm64
```

Observação: o script de release atual publica em `Debug`. Não documente uma release como `Release` sem ajustar e validar o script.

---

# Execução em debug

O projeto de inicialização deve ser `MpvNet.Windows`.

Pontos a validar:

- o executável localiza `libmpv-2.dll`;
- o executável localiza `MediaInfo.dll`;
- `Locale` está disponível quando necessário;
- `mpv.conf`, `mpvnet.conf` e `input.conf` são resolvidos pela pasta correta;
- a janela principal abre;
- reprodução de mídia local funciona;
- fullscreen, menu de contexto e atalhos funcionam.

---

# Release e empacotamento

Script principal:

```text
src/Tools/release-mpv.net.ps1
```

Uso esperado pelo cabeçalho do script:

```powershell
src\Tools\release-mpv.net.ps1 <diretorio-src> <diretorio-saida>
```

Exemplo:

```powershell
src\Tools\release-mpv.net.ps1 C:\repo\mpv.net\src C:\saida
```

O script:

1. valida `MpvNet.sln`;
2. valida 7-Zip e Inno Setup;
3. publica `MpvNet.Windows.csproj` para `win-x64`;
4. publica `MpvNet.Windows.csproj` para `win-arm64`;
5. cria nomes com base na versão do `mpvnet.exe`;
6. copia arquivos publicados;
7. copia `mpvnet.com`, `libmpv-2.dll` e `MediaInfo.dll`;
8. copia `Locale`;
9. cria `portable_config` com modelos comentados de `mpv.conf` e `input.conf`;
10. gera ZIP portátil x64 e ARM64;
11. executa `Setup/Inno/inno-setup.iss`;
12. cria release no GitHub usando `gh release create`.

Pendente real: validar um pacote gerado pelo script completo, incluindo ZIP, instalador e publicação.

---

# Versão

A versão atual do executável está em `src/MpvNet.Windows/MpvNet.Windows.csproj`:

```xml
<FileVersion>7.1.2.0</FileVersion>
<AssemblyVersion>7.1.2.0</AssemblyVersion>
<InformationalVersion>7.1.2.0</InformationalVersion>
```

O script de release usa a versão do arquivo publicado para montar os nomes dos artefatos.

---

# Possíveis problemas comuns

## SDK ou runtime ausente

Confira os `TargetFramework` dos projetos e instale o SDK/runtime correspondente.

## Dependência nativa ausente

Se a aplicação compilar mas não abrir ou falhar ao iniciar reprodução, verifique `libmpv-2.dll`, `MediaInfo.dll`, arquitetura x64/ARM64 e diretório de execução.

## Ferramenta de release ausente

O script falha se 7-Zip, Inno Setup ou `gh` não estiverem nos locais esperados.

## Caminho fixo do Inno Setup

`src/Setup/Inno/inno-setup.iss` usa `OutputDir=E:\Desktop`. Ajustar esse caminho exige cuidado para não quebrar o fluxo original.

---

# Checklist de validação manual

Após compilar:

- abrir aplicação sem argumentos;
- abrir arquivo de vídeo;
- abrir arquivo de áudio;
- abrir imagem;
- abrir URL;
- abrir múltiplos arquivos;
- testar play/pause;
- testar fullscreen;
- testar menu de contexto;
- testar editor de configuração;
- testar editor de input;
- abrir pasta de configuração;
- alterar uma opção simples;
- fechar e abrir novamente;
- verificar persistência;
- validar tema claro/escuro;
- validar modo portátil com `portable_config`;
- validar `MPVNET_HOME`.

---

# Pendências deste guia

- Rodar e registrar resultado de `dotnet build src\MpvNet.sln`.
- Rodar e registrar resultado de `dotnet publish` x64/ARM64.
- Validar execução com dependências nativas reais.
- Validar geração de ZIP e instalador.
- Validar se o pacote portátil gerado inclui `portable_config` por padrão.
