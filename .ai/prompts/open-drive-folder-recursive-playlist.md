# Prompt para o Codex — Abrir unidade/pasta e subpastas com playlist

## Objetivo

Analise e implemente no MPV.NET Media Player a ampliação da opção de menu já existente para selecionar uma unidade ou pasta e localizar recursivamente as mídias reproduzíveis em todas as subpastas. Mantenha uma única entrada de menu: em unidades e pastas comuns, carregue juntos os arquivos de áudio e vídeo reconhecidos e forme uma playlist ordenada para reprodução contínua; em unidades ou estruturas de DVD/Blu-ray reconhecidas, preserve a reprodução especial do disco. Não crie entradas separadas para somente áudio ou somente vídeo, nem um submenu de filtros nesta tarefa.

A tarefa deve ser feita no repositório atual, seguindo AGENTS.md, .ai/README.md, .ai/skills/mpvnet-maintainer.md e as instruções dos agentes especializados. O código e a documentação atuais são a fonte de verdade.

## Entendimento inicial confirmado no código atual

Confirme cada ponto antes de editar, pois ele descreve o estado observado na branch analisada:

- src/MpvNet/Utilities/InputHelp.cs declara o item de menu “Open DVD/Blu-ray Drive/Folder...” e envia o comando script-message-to mpvnet open-optical-media.
- src/MpvNet.Windows/Commands/GuiCommand.Commands.cs associa open-optical-media ao método Open_DVD_Or_BD_Folder.
- src/MpvNet.Windows/Commands/GuiCommand.cs usa FolderBrowserDialog e chama Player.LoadDiskFolder com a pasta escolhida.
- src/MpvNet/Integration/Mpv/Player.MediaLoading.cs faz o método LoadDiskFolderAsync parar a mídia, aguardar, verificar somente se existe a subpasta BDMV diretamente sob o caminho escolhido e então usar bluray-device/bd://; para qualquer outro caminho usa dvd-device/dvd://. Isso significa que uma pasta comum também pode cair no fluxo de DVD.
- O caminho de ISO já tem tratamento separado em LoadISOAsync. Preserve-o.
- LoadFiles tem o parâmetro loadFolder, que arma o carregamento automático de mídia da pasta atual. Player.Events.cs chama LoadFolderAsync quando App.AutoLoadFolder está habilitado. Essa rotina usa Directory.GetFiles sem recursão e o manual descreve esse comportamento como carregamento da mesma pasta.
- FileTypes.GetMediaFiles seleciona formatos de áudio, vídeo e playlists conforme as listas do mpv e os formatos padrão. GetFolderMediaFiles aplica uma regra separada para imagens: quando a mídia atual é uma imagem, adiciona outras imagens; no caso comum, inclui áudio, vídeo e playlists.
- BuildFolderPlaylistItems já expande playlists locais com PlaylistFile.Read e finaliza com PlaylistFile.PrepareForPlayback.
- PlaylistFile.PrepareForPlayback normaliza títulos e elimina caminhos/URLs repetidos usando GetAddressKey, com comparação case-insensitive. GetAddressKey usa Path.GetFullPath para caminhos locais. Arquivos diferentes com o mesmo nome, mas em pastas diferentes, são entradas diferentes.
- PlaylistFile.GetDisplayTitle usa o título fornecido pela playlist quando existe; caso contrário parte do nome do arquivo. TitleHelp.NormalizeMediaTitle remove extensões reconhecidas e normaliza espaços, pontuação e capitalização.
- PlaylistFile.Read reconhece os formatos de playlist declarados em FileTypes.Playlist e filtra as entradas para mídia de áudio/vídeo ou URL reproduzível.
- Os testes em src/MpvNet.Tests cobrem filtros de pasta, playlists, deduplicação e normalização de títulos, mas não há cobertura equivalente para varredura recursiva de diretórios nem para a classificação do fluxo óptico.

Use este resumo como ponto de partida. Reabra os arquivos atuais e leia também docs/manual.md, docs/guia-operacional.md, docs/developer/architecture.md, docs/developer/windows-ui.md, docs/developer/mpv-integration.md e docs/developer/localization.md, além dos testes e scripts pertinentes.

## Requisitos funcionais

1. Atualize o texto da entrada de menu existente usando a mensagem-fonte e as traduções especificadas na seção “Texto e localização do menu”. Mantenha uma única entrada para esta operação: ela carrega áudio e vídeo juntos quando a seleção é uma pasta/unidade comum, e continua aceitando DVD/Blu-ray pelo fluxo óptico. Não crie item exclusivo de áudio, item exclusivo de vídeo ou submenu para escolher tipos.
2. Preserve o identificador de comando open-optical-media e os bindings existentes sempre que possível. Se uma alteração do identificador for tecnicamente necessária, mantenha alias retrocompatível e cubra-o com teste. Não altere input.conf, mpv.conf, opções do usuário nem os valores existentes de configuração para habilitar a varredura recursiva.
3. Classifique o caminho escolhido antes de iniciar a varredura:
   - Unidade óptica física com mídia acessível e estrutura de DVD/Blu-ray reconhecida deve continuar usando dvd-device/dvd:// ou bluray-device/bd://, conforme o disco.
   - Pasta de DVD/Blu-ray válida deve continuar usando a reprodução de disco do mpv/libmpv. Considere os casos em que o usuário seleciona a raiz do disco, a própria pasta BDMV ou VIDEO_TS, segundo o que o código e o mpv realmente suportam.
   - Pasta comum, unidade removível comum ou unidade de dados deve ser percorrida como uma coleção de arquivos. Não envie uma pasta comum para dvd-device/dvd://.
   - Nunca acrescente à playlist comum os segmentos internos de uma estrutura reconhecida de DVD/Blu-ray, como VOB/M2TS, como se fossem arquivos de filmes independentes. Não invente uma playlist de vários discos se a integração atual com mpv não oferecer um fluxo seguro para isso. Defina e documente o comportamento de estruturas ópticas encontradas dentro de uma pasta de coleção.
   - Preserve o caminho de abertura de ISO existente e teste qualquer interação necessária.
4. Para uma unidade/pasta comum, percorra todos os níveis descendentes e carregue juntos os arquivos de áudio e vídeo reconhecidos pelo projeto, além de expandir playlists conforme as regras existentes. Reuse FileTypes, as extensões dinâmicas e padrão, e não inclua legendas, scripts, arquivos desconhecidos ou diretórios como itens da playlist. Não separe áudio e vídeo em comandos, modos ou filtros distintos nesta tarefa.
5. Defina explicitamente o comportamento de imagens. Não misture imagens indiscriminadamente em uma playlist de vídeos/músicas se isso contrariar a política atual de pastas. Preserve o fluxo de imagens já existente e faça a regra da nova abertura coerente com os métodos FileTypes.GetMediaFiles e FileTypes.GetFolderMediaFiles. Registre a decisão na documentação e no resumo final.
6. Expanda playlists encontradas na árvore usando PlaylistFile.Read e mantenha a semântica já estabelecida para entradas válidas e títulos de playlists. Combine os arquivos encontrados diretamente com as entradas expandidas e aplique PlaylistFile.PrepareForPlayback ao conjunto final.
7. Preserve ordem previsível. Use a ordenação natural já adotada pelo projeto em Windows, StringLogicalComparer, aplicada de forma consistente aos caminhos relativos à raiz. A reprodução começa pelo primeiro item da ordem final e avança pelo restante. Não dependa da ordem retornada pelo sistema de arquivos.
8. Remova duplicatas pela identidade de caminho já usada pelo player, de forma case-insensitive, incluindo repetição entre descoberta direta e expansão de playlists. Não deduplique arquivos distintos apenas por nome, tamanho ou título; arquivos homônimos em pastas diferentes devem continuar disponíveis. Use o mecanismo central existente em vez de criar uma segunda política de identidade.
9. Normalize o título de cada arquivo pelo mecanismo existente. Preserve títulos explícitos de playlist válidos; para arquivos comuns, use o nome base sem extensão reconhecida e as regras de TitleHelp. Não mostre caminhos completos como título. Evite substituir nomes distintos por um título vazio ou por um valor genérico sem necessidade.
10. A abertura explícita de uma unidade/pasta sempre executa a varredura recursiva solicitada pelo usuário, independentemente da opção AutoLoadFolder. Mantenha AutoLoadFolder com a finalidade atual de adicionar itens da mesma pasta ao abrir um único arquivo. Não mude essa configuração, não a habilite silenciosamente e evite que o carregamento automático acrescente duplicatas após a varredura recursiva.
11. Mantenha a semântica normal de substituição da playlist para o comando de abertura atual. Só substitua a mídia/playlist em reprodução depois de concluir uma varredura válida com itens utilizáveis. Uma pasta vazia ou sem mídias deve produzir uma mensagem localizada e deixar a reprodução atual intacta.
12. Faça a travessia fora da thread de interface. Não bloqueie a janela enquanto percorre unidades grandes. Trate separadamente erros de acesso, diretórios removidos, falhas de I/O e unidade não pronta: ignore o subdiretório que falhou, continue com os demais quando possível e registre diagnóstico útil. Evite seguir junctions, links simbólicos ou outros reparse points de forma que a varredura saia da raiz ou entre em ciclo. Não imponha limite arbitrário de profundidade ou quantidade sem justificativa concreta.
13. Evite uma chamada LoadFiles por arquivo. Prepare a coleção completa e encaminhe-a ao fluxo central de playlist como uma operação, usando loadFolder=false para não disparar o autoload da mesma pasta. Considere que LoadFiles tem uma proteção temporal que pode transformar uma chamada seguinte em append; a nova operação não deve depender de diversas chamadas próximas.
14. Não pare a reprodução corrente antes de validar o tipo de seleção e concluir a varredura comum. Para falha ou resultado vazio, informe o usuário no idioma atual. Para erro parcial, carregue os itens acessíveis e registre contagens e falhas sem inundar o log com uma linha por arquivo.
15. Atualize docs/manual.md para descrever a nova abertura recursiva e distingui-la do AutoLoadFolder, que continua limitado ao comportamento de mesma pasta. Prefira atualizar documentação existente e não crie documentação redundante.

## Localização

- Use como msgid em InputHelp.cs exatamente o texto em inglês da tabela abaixo. Inclua essa nova mensagem em lang/source.pot e traduza-a em todos os catálogos de idioma atualmente existentes em lang/po: bg.po, zh_CN.po, es.po, fr.po, de.po, it.po, ja.po, ko.po, pl.po, pt_BR.po, pt_PT.po, ru.po e tr.po. Inglês continua sendo nativo, representado pelo msgid em source.pot; não crie lang/po/en.po.
- Use as traduções propostas na tabela como base e revise-as no contexto do menu. Atualize cada arquivo .po existente; não deixe nenhum idioma traduzido sem a nova entrada, vazia ou com tradução copiada em inglês.
- Siga lang/update-gettext-catalogs.ps1 e o fluxo atual de lang/source.pot/lang/po. Preserve placeholders, regras de fallback, os identificadores de idioma e a paridade dos catálogos.
- Execute lang/validate-po-files.ps1 -ValidateOnly e os passos de compilação de catálogo documentados, conforme o ambiente permitir. Não deixe traduções duplicadas ou fora de sincronia com source.pot.

## Texto e localização do menu

O menu mantém uma única opção para abrir mídia de unidades/pastas. O rótulo comunica que a varredura inclui subpastas; a abertura de DVD/Blu-ray permanece disponível por detecção interna do tipo selecionado. Use exatamente este msgid-fonte e as traduções abaixo ao atualizar source.pot e cada arquivo .po:

| Idioma | msgid/tradução |
| --- | --- |
| Inglês (nativo) | Open media from a drive or folder (including subfolders)... |
| Búlgaro (bg.po) | Отваряне на медия от устройство или папка (включително подпапките)... |
| Chinês simplificado (zh_CN.po) | 打开驱动器或文件夹中的媒体（包括子文件夹）... |
| Espanhol (es.po) | Abrir medios desde una unidad o carpeta (incluidas las subcarpetas)... |
| Francês (fr.po) | Ouvrir des médias depuis un lecteur ou un dossier (y compris les sous-dossiers)... |
| Alemão (de.po) | Medien von einem Laufwerk oder Ordner öffnen (einschließlich Unterordnern)... |
| Italiano (it.po) | Apri i contenuti multimediali da un'unità o cartella (incluse le sottocartelle)... |
| Japonês (ja.po) | ドライブまたはフォルダーからメディアを開く（サブフォルダーを含む）... |
| Coreano (ko.po) | 드라이브 또는 폴더에서 미디어 열기(하위 폴더 포함)... |
| Polonês (pl.po) | Otwórz multimedia z dysku lub folderu (wraz z podfolderami)... |
| Português do Brasil (pt_BR.po) | Abrir arquivos de mídia em uma unidade ou pasta (incluindo subpastas)... |
| Português de Portugal (pt_PT.po) | Abrir ficheiros multimédia numa unidade ou pasta (incluindo subpastas)... |
| Russo (ru.po) | Открыть медиафайлы с диска или из папки (включая вложенные папки)... |
| Turco (tr.po) | Bir sürücüden veya klasörden medya aç (alt klasörler dâhil)... |

O label pode ser ajustado somente se a análise de espaço do menu mostrar um problema concreto; nesse caso, atualize o msgid e todas as traduções no mesmo trabalho, mantendo o mesmo sentido em todos os idiomas. Não altere as regras de idiomas/fallback do projeto. A implementação deve comprovar que os arquivos de catálogo foram atualizados e compilados conforme a documentação, quando o ambiente permitir.

## Testes exigidos

Amplie a cobertura existente sem depender de uma unidade óptica física para testes automatizados. Prefira separar a descoberta/classificação de mídia em lógica testável, sem refatoração ampla. Cubra, no mínimo:

- descoberta de mídia em múltiplos níveis, incluindo áudio e vídeo e excluindo tipos não aceitos;
- ordenação natural e estável independente da ordem de enumeração do sistema de arquivos;
- expansão de playlists encontradas em subpastas;
- deduplicação entre arquivo encontrado diretamente e a mesma entrada listada em playlist;
- preservação de arquivos distintos com o mesmo nome em diretórios diferentes;
- normalização de títulos de arquivo e preservação de título fornecido por playlist;
- pasta comum classificada para varredura, não para dvd://;
- classificação de raiz DVD/Blu-ray e seleção da própria BDMV/VIDEO_TS, de acordo com o fluxo realmente suportado;
- exclusão de arquivos internos de estruturas ópticas da playlist de coleção;
- pastas vazias, raiz inválida e erro de acesso parcial sem encerrar o aplicativo;
- AutoLoadFolder ligado/desligado sem interferir na abertura recursiva nem gerar duplicatas;
- preservação da playlist corrente se a varredura não produzir itens utilizáveis.

Use os projetos e harnesses existentes; confira CoreCoverageTests.cs, Program.cs e MediaInputTests.cs antes de decidir onde cada teste pertence. Execute a validação leve documentada: dotnet run --project src/MpvNet.Tests/MpvNet.Tests.csproj --no-restore. Execute também dotnet build src/MpvNet.Windows/MpvNet.Windows.csproj --no-restore e os validadores de localização quando as ferramentas e o ambiente estiverem disponíveis. Faça git diff --check e revise o diff final.

## Roteiro manual no Windows

Documente o resultado e valide, quando houver ambiente apropriado:

1. Abrir uma pasta comum com mídia em várias camadas de subpastas; confirmar a ordem, o título, a playlist e a passagem automática ao próximo item.
2. Abrir uma unidade de dados/removível com subpastas e uma pasta sem mídias.
3. Abrir pasta com arquivos homônimos em subpastas diferentes.
4. Abrir playlist dentro de subpasta e verificar título, expansão e ausência de repetidos.
5. Abrir uma pasta com permissões parciais ou remover uma unidade durante a leitura; confirmar que o app continua responsivo e carrega o que foi encontrado.
6. Abrir uma unidade óptica com DVD e outra com Blu-ray, se disponíveis; confirmar que a reprodução especial, títulos/capítulos e navegação do disco continuam funcionando.
7. Confirmar que DVD/Blu-ray em pasta e ISO continuam sendo tratados pelo fluxo correto.
8. Repetir os testes com AutoLoadFolder ligado e desligado; confirmar que a nova varredura não duplica itens e que a abertura individual continua respeitando a opção.
9. Verificar menu, tradução, DPI, tema claro/escuro, tela cheia, atalhos e drag/drop conforme AGENTS.md.

Se não houver drive óptico ou Windows disponível, registre isso como não validado em ambiente, sem afirmar que a integração física foi testada.

## Restrições de manutenção

- Preserve compatibilidade com mpv/libmpv, arquivos de configuração existentes, comandos e playlists atuais.
- Evite refatoração ampla e mudanças não relacionadas.
- Não altere o comportamento de abrir arquivos, URL, clipboard, drag/drop, playlists ou imagens fora do escopo, exceto para compartilhar uma correção central comprovada pelos testes.
- Não acrescente dependências sem necessidade.
- Não faça commit, push, release ou publicação sem solicitação explícita.
- Atualize documentação só com o comportamento efetivamente implementado e validado.

## Resumo obrigatório ao concluir a implementação

Informe:
1. o fluxo atual confirmado e o que mudou;
2. os arquivos principais alterados;
3. como foram classificados unidades, pastas comuns, DVD/Blu-ray e ISO;
4. a política de tipos de mídia, títulos, ordenação e deduplicação;
5. os testes executados e seus resultados;
6. o checklist manual realizado e o que depende de Windows/unidade física;
7. qualquer limitação que ainda exista.
