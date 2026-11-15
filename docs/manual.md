# Manual do MPV.NET Media Player

## Tópicos

- [Instalar](#instalação)
- [Configurar](#configuração)
- [Abrir mídia e controlar a reprodução](#controles-de-reprodução)
- [Usar a linha de comando](#linha-de-comando)
- [Usar o modo portátil](#modo-portátil)
- [Obter suporte](#suporte)

## Download

Escolha o instalador ou o ZIP portátil nas [releases do GitHub](https://github.com/WandersondeSouza/mpv.net/releases). O player também está na [Microsoft Store](https://apps.microsoft.com/detail/9N441SP6XHLD).

## Instalação

O player requer Windows x64, a partir do Windows 10 versão 2004 (build 19041).
O instalador e o ZIP das releases incluem o runtime. Compilações que dependem
de um runtime externo usam o .NET Desktop Runtime 10.

O instalador registra formatos de áudio, vídeo e playlist e adiciona
`mpvnet.exe` ao `PATH` para novas sessões de terminal. O ZIP pode ser
extraído e executado diretamente. A versão da Store usa o alias
`mpvnet.exe` do Windows.

### Extensões e associações de arquivos

O player abre vídeos, áudios, imagens e playlists, como `.mp4`, `.mkv`,
`.flac`, `.png`, `.m3u` e `.m3u8`. A reprodução depende dos
codecs da versão de libmpv. Consulte a [matriz de formatos e associações](developer/mpv-integration.md#matriz-de-filtros-descoberta-e-associações).

Na instalação desktop, para associar imagens, use **Configuração →
Configuração inicial → Registrar associações de arquivos de imagem**. Para
escolher o aplicativo padrão no Windows, use **Abrir com** no Explorador ou
**Configurações → Aplicativos → Aplicativos padrão**. O pacote da Store
declara também as extensões de imagem. O ZIP portátil deixa as associações a
cargo do usuário.

## Configuração

1. Clique com o botão direito na janela do player e escolha
   **Configuração → Mostrar editor de configuração** (`Ctrl+,`).
2. Escolha uma categoria ou use o campo de busca para localizar a opção.
3. Feche o editor para salvar. Reinicie o player após mudar o idioma ou o tema.

| Para ajustar | Onde encontrar no editor |
| --- | --- |
| Idioma da interface | **Aparência → `language`** |
| Tema claro ou escuro | **Aparência → `dark-mode`** |
| Idiomas de áudio | **Áudio → `alang`** |
| Idiomas de legendas | **Legenda → `slang`** |
| Busca e cache de capas | **Áudio**, nas opções de capas |
| Carregamento automático de arquivos vizinhos | **Reprodução → `auto-load-folder`** |
| Cache de streaming | **Cache → Cache automático de streaming** |
| URLs no histórico | **Geral → `save-network-urls-in-recent`** |

Para personalizar atalhos, abra **Configuração → Mostrar editor de atalhos**
(`Ctrl+i`). Veja [como personalizar ou desativar um atalho](ATALHOS.md#personalizar-ou-desativar-um-atalho).

### Idiomas

O player usa o idioma do Windows quando há um mapeamento disponível e recorre
ao inglês nos demais casos. Você pode pré-visualizar outro idioma no editor
antes de fechar a janela. Consulte os [20 idiomas e as variantes aceitas](developer/localization.md#idiomas-disponíveis).

Em `alang` e `slang`, informe os códigos em ordem de preferência, como
`pt-BR,por,eng`. O mpv escolhe entre as faixas disponíveis. Uma preferência
em `alang` também pode influenciar o idioma da interface no início da sessão.
Para mudar a faixa atual ou selecionar **Nenhuma legenda**, use **Faixa**.

### Pasta e ajustes avançados

Use **Configuração → Abrir pasta de configuração** (`Ctrl+f`) para
manter cópias de segurança, instalar scripts ou editar opções avançadas.
O editor salva as opções no `mpv.conf` e no `mpvnet.conf` da pasta ativa.
Para trocar de perfil, consulte a [ordem de busca da pasta de configuração](CONFIGURACAO.md#ordem-de-busca).
`mpvnet.exe --diagnose-paths` mostra os caminhos efetivos sem criar pastas.

## Controles de reprodução

| Ação | Controle padrão |
| --- | --- |
| Pausar ou continuar | Clique esquerdo na área de vídeo, `Space` ou `p` |
| Alternar tela cheia | Duplo clique, `Enter`, `f` ou botão **Tela cheia** na barra de título |
| Fechar o menu, sair da tela cheia ou fechar o player | `Esc` |
| Abrir o menu de contexto | Clique direito |
| Escolher um item da playlist | `F8` |

O clique simples aguarda o intervalo de duplo clique do Windows para distinguir
as duas ações. Consulte a [lista completa de atalhos](ATALHOS.md).

### Abrir arquivos, playlists e pastas

Arraste a mídia para a janela ou selecione **Arquivo → Abrir arquivos...**.
Para montar uma playlist a partir de uma unidade ou pasta inteira, use
**Arquivo → Abrir arquivos de mídia em uma unidade ou pasta (incluindo
subpastas)...**. O player ordena pelo caminho relativo em ordem natural,
expande playlists locais e evita entradas duplicadas. Se a busca não encontrar
mídia utilizável, a fila atual é preservada.

Para adicionar só os arquivos vizinhos ao abrir um arquivo local, ative
`auto-load-folder` em **Reprodução**. Ao abrir uma imagem, essa opção inclui
apenas outras imagens da mesma pasta. Discos com `VIDEO_TS` ou `BDMV`
podem ser abertos diretamente. Veja as [regras de descoberta de mídia](developer/mpv-integration.md#abertura-explícita-de-unidadepasta).

### Abrir streams e páginas de mídia

Cole a URL com `Ctrl+v` ou [passe-a na linha de comando](#linha-de-comando).
Para páginas compatíveis, como YouTube ou Vimeo, o mpv usa o yt-dlp. Links do
YouTube com `list=` podem abrir a playlist. O acesso depende da versão do
extrator, do serviço e do conteúdo. Alguns vídeos exigem autenticação ou têm
proteção DRM. Mantenha cookies e URLs privadas fora dos logs compartilhados.
Consulte o [diagnóstico de streaming](developer/mpv-integration.md#diagnóstico-da-cadeia-online)
e a [orientação do yt-dlp sobre cookies](https://github.com/yt-dlp/yt-dlp/wiki/Extractors#exporting-youtube-cookies).

**Cache → Cache automático de streaming** vem ativado. A mudança vale para o
próximo item. Opções explícitas do mpv têm prioridade. A retomada, quando
aplicável a uma falha transitória de live, usa até três tentativas. Consulte
[limites, migração e recuperação](developer/mpv-integration.md#cache-automático-por-item).

**Geral → `save-network-urls-in-recent`** vem desativado. Se ativar, use
apenas URLs cujos parâmetros você pode expor. O histórico fica em texto
legível. O filtro exclui URLs com credenciais, fragmentos ou parâmetros
sensíveis reconhecidos.

O player prepara os componentes online em segundo plano e usa a rede para
baixar os que estiverem ausentes ou vencidos. Para conferir as versões, execute
`mpvnet.exe --diagnose-components`. O diagnóstico não baixa arquivos.
A preparação de dependências e os caminhos estão em
[Componentes auxiliares de runtime](developer/configuration.md#componentes-auxiliares-de-runtime).

## Controles de mídia do Windows

Os controles do Windows podem mostrar título, capa e posição e enviar comandos
de reprodução ao player. A barra da miniatura oferece anterior, próximo,
capítulos e Play/Pause. Ela também tem um botão para apoiar o projeto.
A busca depende da capacidade da mídia. Os controles do sistema continuam
ativos em tela cheia. Consulte a [integração do Windows](developer/windows-ui.md#controles-de-mídia-do-windows).

## Capas durante a reprodução de áudio

Para áudio local, o player usa a capa selecionada pelo mpv. Na falta dela,
pode buscar a capa do álbum e uma foto verificada do artista antes de usar o
ícone do aplicativo. A imagem pode aparecer na janela e nos controles do
Windows. As preferências explícitas de faixa e o vídeo da mídia têm prioridade.

- Em **Áudio → Buscar capas de áudio online**, selecione **Não** para desativar
  novas consultas. Capas já armazenadas podem continuar disponíveis.
- Em **Áudio → Manter cache de capas entre sessões**, selecione **Não** para
  usar arquivos temporários daquela sessão. Feche o editor e reinicie após
  alterar essas opções.

A busca online fica restrita a arquivos locais disponíveis. As consultas podem
enviar nomes de artista e álbum, identificadores das tags e o IP da conexão.
Para usar uma imagem de sua escolha, selecione a faixa de vídeo no menu
**Faixa** ou passe `--cover-art-file` ao abrir o áudio. Consulte
[as opções avançadas de capas](developer/configuration.md#mpvnetconf) e
[como o player seleciona e armazena as imagens](developer/windows-ui.md#controles-de-mídia-do-windows).

## Visibilidade dos controles OSC

Pressione `Del` ou use **Exibir → Alternar visibilidade do OSC** para alternar
entre automático, sempre visível e oculto durante a sessão.

Para começar sempre com os controles visíveis e reservar espaço para eles no
layout padrão, crie ou edite `script-opts/osc.conf` na
[pasta de configuração](#pasta-e-ajustes-avançados). Acrescente as opções
abaixo e reinicie o player.

```ini
visibility=always
boxvideo=yes
sub_margins=yes
```

Preserve as demais opções do arquivo. Para voltar ao padrão, remova essas
linhas ou use `visibility=auto`, `boxvideo=no` e `sub_margins=no`.
Legendas com posicionamento próprio podem exigir ajuste.

## Linha de comando

No PowerShell, execute os comandos na pasta do player. Se o
`mpvnet.exe` estiver no `PATH`, você pode omitir o prefixo `.\`.

```powershell
.\mpvnet.exe "C:\Videos\filme.mp4"
.\mpvnet.exe "C:\Listas\iptv.m3u"
.\mpvnet.exe "https://example.com/live/index.m3u8"
```

### Integração por parâmetros: vídeo, título e idioma

Para abrir uma mídia com título e idioma definidos, use a forma abaixo.
Substitua o caminho local por uma URL quando necessário.

```powershell
& "C:\Players\MPV.NET\mpvnet.exe" --process-instance=multi --alang= --language=pt-BR --title="Meu vídeo" "C:\Videos\video.mp4"
```

- `--process-instance=multi` abre uma nova instância e aplica as opções a ela.
- `--alang=` limpa a preferência de áudio só para essa execução, evitando
  que ela altere o idioma escolhido para a interface.
- `--language=pt-BR` define a interface. Aceita os
  [20 códigos e nomes públicos](developer/localization.md#idiomas-disponíveis).
  O último `--language` informado prevalece. Um código desconhecido usa inglês.
  Omita a opção para usar a preferência salva.
- `--title` define o título mostrado pelo player. Para URLs, também é possível
  passar o título como primeiro argumento posicional.

<details>
<summary>Exemplo com variáveis no PowerShell 7.3 ou posterior</summary>

```powershell
$player = 'C:\Players\MPV.NET\mpvnet.exe'
$video = 'C:\Videos\video.mp4'
$titulo = 'Meu vídeo'
$idioma = 'pt-BR'

if ([string]::IsNullOrWhiteSpace($idioma)) {
    $idioma = 'en'
}

$argumentosPlayer = @(
    '--process-instance=multi'
    '--alang='
    "--language=$idioma"
    "--title=$titulo"
    $video
)

Push-Location -LiteralPath (Split-Path -LiteralPath $player)
try {
    & $player @argumentosPlayer
} finally {
    Pop-Location
}
```

</details>

## Modo portátil

Extraia o ZIP completo, incluindo DLLs, idiomas e scripts. Use
[`portable_config`](CONFIGURACAO.md#estrutura-portátil) ao lado do executável
para manter as preferências junto do player. Cache, componentes e logs usam
a raiz de dados local. Confira os caminhos com `mpvnet.exe --diagnose-paths`.

## Documentação técnica

- [Arquitetura](developer/architecture.md)
- [Configuração e componentes](developer/configuration.md)
- [Integração com mpv/libmpv](developer/mpv-integration.md)
- [Interface Windows](developer/windows-ui.md)
- [Build e release](developer/build-release.md)
- [Localização](developer/localization.md)

## Suporte

- [Relatar problemas ou sugerir melhorias](https://github.com/WandersondeSouza/mpv.net/issues)
- [Coletar logs de diagnóstico](logging.md)
- [Enviar um relato de segurança em privado](../SECURITY.md)
- [Consultar opções e comandos do mpv](https://mpv.io/manual/master/)
- [Apoiar o projeto](https://www.gestaodesistemas.com.br/mpvnet). A contribuição é voluntária.
