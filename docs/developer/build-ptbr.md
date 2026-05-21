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
- `libmpv-2.dll` x64;
- `MediaInfo.dll` x64 versionada em `src/Native/win-x64/MediaInfo.dll`;
- `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `yt-dlp.exe` no pacote portatil do fork;
- arquivos de `Locale`, quando aplicável.

Para release:

- 7-Zip em `C:\Program Files\7-Zip\7z.exe`;
- Inno Setup 6 em `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`, exceto quando `-SkipInstaller` for usado;
- GitHub CLI (`gh`), exceto quando `-SkipGitHubRelease` for usado;
- variável `GH_TOKEN` configurada para criação de release;
- acesso a internet para baixar FFmpeg, libmpv, yt-dlp, `mpvnet.com` e `Gettext.Tools` no momento da release, quando esses arquivos/ferramentas ainda nao estiverem disponiveis localmente.

Observacao: Inno Setup, GitHub CLI e `GH_TOKEN` deixam de ser obrigatorios quando o script e executado, respectivamente, com `-SkipInstaller` e `-SkipGitHubRelease`.

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
| `src/MpvNet.Windows/MpvNet.Windows.csproj` | aplicação Windows | `net10.0-windows7.0`, `win-x64` | `mpvnet.exe` x64 |
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
6. Confirme que `src/Native/win-x64/MediaInfo.dll` existe. O build copia esse arquivo para a saida da aplicacao. No fluxo de release, `libmpv-2.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `yt-dlp.exe` sao baixados automaticamente para o pacote portatil.

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

O projeto da aplicação define `RuntimeIdentifier=win-x64` e `Prefer32Bit=false`, portanto o build normal gera o executável como x64.

Para publicar como o script de release atual faz:

```powershell
dotnet publish src\MpvNet.Windows\MpvNet.Windows.csproj --self-contained false --configuration Debug --runtime win-x64
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

Por padrao, o script publica em `WandersondeSouza/mpv.net`. Para gerar apenas artefatos locais, use:

```powershell
src\Tools\release-mpv.net.ps1 C:\repo\mpv.net\src C:\saida -SkipGitHubRelease
```

Para gerar apenas o ZIP portatil, sem instalador e sem publicacao:

```powershell
src\Tools\release-mpv.net.ps1 C:\repo\mpv.net\src C:\saida -SkipInstaller -SkipGitHubRelease
```

Quando for necessario sobrescrever `MediaInfo.dll` ou fornecer um `mpvnet.com` local, informe os arquivos explicitamente:

```powershell
src\Tools\release-mpv.net.ps1 C:\repo\mpv.net\src C:\saida -MediaInfoFile C:\deps\MediaInfo.dll -MpvNetComFile C:\deps\mpvnet.com
```

O script:

1. valida `MpvNet.sln`;
2. valida 7-Zip e, quando aplicavel, Inno Setup;
3. publica `MpvNet.Windows.csproj` para `win-x64`;
4. cria nomes com base na versão do `mpvnet.exe`;
5. copia arquivos publicados;
6. baixa `ffmpeg-master-latest-win64-gpl.zip` do BtbN e copia `ffmpeg.exe`, `ffplay.exe` e `ffprobe.exe`;
7. baixa `mpv-dev-x86_64-...7z` do shinchiro e copia `libmpv-2.dll`;
8. baixa `yt-dlp.exe` do release latest oficial do yt-dlp;
9. copia `MediaInfo.dll` da pasta `src/Native/win-x64`, baixa ou copia `mpvnet.com`, e copia `libmpv-2.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `yt-dlp.exe` x64;
10. copia `Locale`;
11. cria `portable_config` com modelos comentados de `mpv.conf` e `input.conf`;
12. gera ZIP portátil x64;
13. executa `Setup/Inno/inno-setup.iss` para gerar o instalador x64, exceto com `-SkipInstaller`;
14. cria release no GitHub usando `gh release create`, exceto com `-SkipGitHubRelease`.

As dependencias baixadas automaticamente usam estas fontes:

- FFmpeg: `https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest`, asset `ffmpeg-master-latest-win64-gpl.zip`;
- libmpv: `https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest`, asset `mpv-dev-x86_64-[data]-git-[hash].7z`;
- yt-dlp: `https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe`.
- Gettext.Tools: `https://api.nuget.org/v3-flatcontainer/gettext.tools/`, usado para obter `msgfmt.exe` e gerar `Locale` quando `msgfmt.exe` nao esta no `PATH`.

O script usa o asset x64 generico de libmpv, nao `x86_64-v3`, para preservar compatibilidade com mais CPUs x64. Se o GitHub mudar os nomes dos assets, se o NuGet mudar a resolucao de `Gettext.Tools`, se o download falhar, se a extracao falhar ou se algum arquivo baixado estiver vazio, o script deve abortar antes de gerar um pacote parcial. `MediaInfo.dll` e uma dependencia versionada do fork, mas pode ser sobrescrita por `-MediaInfoFile`.

O workflow manual `.github/workflows/release-packages.yml` executa esse mesmo script no GitHub Actions. Ele sempre publica os pacotes como artefato do workflow e, quando executado com `create_release=true`, tambem cria a Release no repositorio. `MediaInfo.dll` ja esta versionada no repositorio e nao exige secret.

Este fork nao publica um pacote NuGet/container no GitHub Packages por enquanto; os pacotes de distribuicao do aplicativo sao assets de GitHub Releases e artefatos do workflow.

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

Se a aplicação compilar mas não abrir ou falhar ao iniciar reprodução, verifique `libmpv-2.dll`, `MediaInfo.dll`, arquitetura x64 e diretório de execução. Para o pacote portatil, verifique tambem `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` e `yt-dlp.exe` ao lado de `mpvnet.exe`. No fluxo de release, FFmpeg, libmpv e yt-dlp devem ser baixados automaticamente; `MediaInfo.dll` deve vir de `src/Native/win-x64/MediaInfo.dll`.

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
- Rodar e registrar resultado de `dotnet publish` x64.
- Validar execução com dependências nativas reais.
- Validar geração de ZIP e instalador.
- Validar se o pacote portátil gerado inclui `portable_config` por padrão.
