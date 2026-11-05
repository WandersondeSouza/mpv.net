# Atalhos

Referência dos atalhos definidos pelo fork em `InputHelp.GetDefaults()`.
As tabelas descrevem uma configuração sem personalizações, com a janela
principal focada. Seu `input.conf`, opções de inicialização e scripts podem
substituir esses controles; confira a lista ativa em caso de diferença.

- `input.conf`: atalhos de teclado e mouse;
- `global-input.conf`: atalhos globais do Windows;
- `input-test` e `input-keylist`: modos de diagnóstico;
- o menu de contexto pode ser ajustado pelo `input.conf` compatível com o fork.

Para localizar os arquivos e a pasta ativa, veja [Configuração](CONFIGURACAO.md).

## Controles padrão de reprodução

| Ação | Binding padrão | Observações |
| --- | --- | --- |
| Play/Pause | `Space`, `p`, `P` | Usam `script-message-to mpvnet play-pause`, assim como as teclas de mídia `Play`, `Pause` e `PlayPause`. |
| Parar | `Ctrl+s`, tecla de mídia `Stop` | Executam `stop`; não fecham o player. |
| Avançar / voltar 5 s | `Right` / `Left` | `seek 5` / `seek -5`. |
| Avançar / voltar 30 s | `Up` / `Down` | `seek 30` / `seek -30`. |
| Avançar / voltar 5 min | `Ctrl+Right` / `Ctrl+Left` | `seek 300` / `seek -300`. |
| Avançar / voltar 1 s exato | `Shift+Right` / `Shift+Left` | Seek exato, sem OSD. |
| Avançar / voltar 5 s exatos | `Shift+Up` / `Shift+Down` | Seek exato, sem OSD. |
| Próximo / anterior quadro | `.` / `,` | `frame-step` / `frame-back-step`. |
| Desfazer seek / marcar posição de retorno | `Shift+BS` / `Shift+Ctrl+BS` | `BS` é Backspace; usam `revert-seek` / `revert-seek mark`. |
| Aumentar / diminuir volume | `+` / `-` | Passos de 2 unidades. |
| Mute | `m` | Alterna `mute`. |
| Diminuir / aumentar velocidade | `[` / `]` | Divide / multiplica a velocidade por 1,1. |
| Metade / dobro da velocidade | `{` / `}` | Multiplica a velocidade por 0,5 / 2. |
| Restaurar velocidade | `BS` | `set speed 1`. |
| Alternar tela cheia | `Enter`, `f` | `F` maiúsculo diminui o tamanho da legenda. |
| Fechar menu / sair de tela cheia / sair do player | `Esc` | Fecha primeiro o menu de contexto aberto; em tela cheia, volta à janela. Fora desses casos, o binding padrão é `quit`. |
| Sair / sair salvando para continuar depois | `q` / `Q` | `quit` / `quit-watch-later`. |

Seek depende da capacidade da mídia. Streams ao vivo sem busca não permitem
voltar ou avançar livremente; um atalho não acrescenta essa capacidade.

## Playlist, faixas, legendas e repetição

| Ação | Binding padrão | Observações |
| --- | --- | --- |
| Arquivo anterior / próximo | `F11` / `F12`, `<` / `>` | Também pelas teclas de mídia `Prev` / `Next`. |
| Primeiro / último arquivo | `Home` / `End` | Navegam na playlist atual. |
| Selecionar item da playlist | `F8`, sequência `g-p` | Pressione `g` e depois `p`; não é uma combinação simultânea. |
| Capítulo seguinte / anterior | `PGUP` / `PGDWN` | Depende dos capítulos da mídia. |
| Próxima faixa de áudio | `KP7` | Tecla 7 do teclado numérico; não é o `7` da fileira superior. |
| Próxima legenda | `KP8` | Tecla 8 do teclado numérico. |
| Selecionar áudio / legenda | `Alt+a` / `Alt+s` | Também pelas sequências `g-a` / `g-s`. |
| Selecionar legenda secundária | `Alt+b`, sequência `g-S` | O `S` da sequência é maiúsculo. |
| Mostrar / ocultar legenda | `v` | Alterna `sub-visibility`, sem remover a faixa. |
| Adiantar / atrasar legenda | `z` / `Z` | Diminui / aumenta `sub-delay` em 0,1 s. |
| Subir / descer legenda | `r` / `R` | Diminui / aumenta `sub-pos` em 1. |
| Diminuir / aumentar legenda | `F` / `G` | Passos de 0,1 em `sub-scale`. |
| Definir / limpar loop A-B | `l` | `ab-loop`: marca A, marca B, depois limpa os pontos. |
| Repetir arquivo indefinidamente | `L` | Alterna `loop-file` entre `inf` e `no`; não é repetição da playlist. |

## Mouse e movimento da janela

Os bindings abaixo valem fora de controles que capturam o mouse. Sobre o OSC,
os botões, a timeline e a roda seguem as ações do próprio painel; scripts também
podem registrar áreas e bindings próprios.

| Ação | Binding padrão | Observações |
| --- | --- | --- |
| Play/Pause na área de vídeo | Clique esquerdo (`MBTN_Left`) | Exige mídia carregada e área elegível fora da reserva do OSC. |
| Alternar tela cheia | Duplo clique esquerdo (`MBTN_Left_DBL`) | Cancela a pausa pendente do clique simples. |
| Play/Pause | Botão do meio (`MBTN_Mid`) | Usa o mesmo comando de teclado. |
| Abrir menu de contexto | Botão direito (`MBTN_Right`) | Os nomes das ações acompanham o idioma da interface. |
| Aumentar / diminuir volume | `Wheel_Up` / `Wheel_Down` | Passos de 2; roda horizontal `Wheel_Right` / `Wheel_Left` também altera volume. |
| Avançar / voltar 5 s | `Ctrl+Wheel_Up` / `Ctrl+Wheel_Down` | Seek sem OSD. |
| Arquivo anterior / próximo | Botões laterais `MBTN_Back` / `MBTN_Forward` | Navegam na playlist. |
| Mover janela | Arraste com botão esquerdo fora da reserva do OSC | Na janela normal, com `window-dragging=yes`; arrastar cancela a pausa pendente. |
| Posicionar janela | `Alt+Left`, `Alt+Right`, `Alt+Up`, `Alt+Down` | Move para a borda correspondente da tela; `Alt+BS` centraliza. |

O clique simples aguarda somente o restante do intervalo de duplo clique do
Windows. Arrastar a janela, trocar a mídia ou fechar o player cancela a pausa
pendente, assim como perder o foco ou abrir o menu de contexto. A reserva do
frontend é uma heurística: exclui os 10% laterais e os 22% inferiores da área
cliente, além dos 10% superiores quando a janela está sem borda. Ela não é uma
consulta das áreas interativas reais do OSC; pode excluir partes do vídeo mesmo
com os controles ocultos. Cliques consumidos pelo OSC ou por scripts continuam
sob controle do mpv.

`p` deixa de mostrar progresso no padrão. O comando `show-progress` continua
disponível no menu **View > Progress** (com o nome traduzido no idioma da
interface) e pode receber um atalho no editor de input.

## Personalizar ou desativar um atalho

Abra o editor de atalhos com `Ctrl+i` (**Config > Show Input Editor**) ou o
arquivo de texto `input.conf` com `k` (**Config > Edit input.conf**). `Ctrl+f`
abre a pasta de configuração ativa. Faça uma cópia do arquivo antes de editar,
preserve as demais linhas e reinicie o player para aplicar as alterações.

Cada linha contém a tecla e o comando. Por exemplo, para fazer a seta direita
avançar 10 segundos:

```ini
Right seek 10
```

Para desativar somente `p`, mantendo `P` e `Space` nos padrões:

```ini
p ignore
```

Maiúsculas e minúsculas são distintas: `f` alterna tela cheia, mas `F` altera a
legenda; `l` e `L` controlam repetições diferentes. O parser normaliza os nomes
dos modificadores (`ctrl+` para `Ctrl+`, por exemplo), mas preserva a caixa da
letra. Use os nomes de tecla da consulta `Alt+k`; símbolos dependem do layout
do teclado.

Em arquivos sem menu completo, o frontend combina seus padrões com o conteúdo
do usuário, deixando bindings explícitos, inclusive `ignore`, com prioridade
sobre os defaults. Evite duas linhas próprias com a mesma tecla e comandos
diferentes; o editor verifica esses conflitos ao salvar.

Um arquivo com a sintaxe de menu ativa (`#menu: ` por padrão) fornece seus
próprios bindings e não recebe a mesclagem geral de defaults. Ao personalizá-lo,
edite a linha existente para preservar seu rótulo de menu; não substitua o
arquivo inteiro por estes exemplos. O frontend preserva as ações essenciais de
abrir arquivos e Sobre no menu. Migrações de comandos/links legados são
separadas e usam backup; não impõem os controles novos. Detalhes em
[Configuração técnica](developer/configuration.md#inputconf).

Se iniciar com `--input-conf=...`, esse arquivo explícito prevalece no mpv. O
editor do frontend continua ligado ao `input.conf` da pasta ativa; confira o
arquivo indicado na linha de comando e consulte os bindings efetivos.
Scripts e OSC podem ter prioridade nas suas seções ativas, mesmo com uma linha
explícita no arquivo.

## J/K/L como configuração opcional

O player mantém os padrões atuais. Para quem prefere voltar, pausar e avançar
com três teclas próximas, este exemplo pode ser colocado voluntariamente no
`input.conf`, após revisar os conflitos:

```ini
j seek -5
k script-message-to mpvnet play-pause
l seek 5
```

Este exemplo usa **letras minúsculas** e saltos relativos de 5 segundos, não
controle de velocidade nem reprodução reversa. Cada execução de `j` ou `l`
solicita um salto; a repetição ao manter a tecla segue o input do mpv, sem
aceleração adicional criada pelo frontend. `k` usa o Play/Pause oficial.
Seek continua limitado pela mídia, inclusive quando pausada ou em buffering;
transmissões sem busca não passam a permitir retrocesso.

| Tecla | Padrão do fork | Com o exemplo opcional |
| --- | --- | --- |
| `j` | Sem binding próprio | Voltar 5 s. |
| `k` | Editar `input.conf` como texto | Play/Pause; o editor visual continua em `Ctrl+i` e a edição como texto no menu Config. |
| `l` | Loop A-B | Avançar 5 s; o loop A-B continua no menu Tools. |
| `J`, `K` | Sem binding próprio | Permanecem sem binding próprio; não são aliases das minúsculas. |
| `L` | Repetir arquivo | Permanece igual. |

Os defaults nativos embutidos do mpv são desabilitados pelo frontend na
inicialização padrão; portanto não se deve inferir ações para `j`/`J`/`K` a
partir de outro player. Scripts, personalizações e opções explícitas ainda podem
atribuir essas teclas: confira a lista ativa antes de usar o exemplo.

Para desfazer, restaure apenas as linhas modificadas do seu arquivo e reinicie.
Em um perfil sem menu completo e sem bindings anteriores nessas teclas, basta
remover as três linhas acrescentadas. Este guia não ativa o exemplo nem grava
configurações por conta própria.

## Consultar e diagnosticar atalhos

Com a janela principal focada, os padrões incluem:

| Tecla | Ação |
| --- | --- |
| `Del` | Alternar a visibilidade do OSC durante a sessão. |
| `F2` | Abrir o seletor de bindings do mpv. |
| `?` | Mostrar/ocultar a lista de atalhos ativos na tela. |
| `Ctrl+i` | Abrir o editor de atalhos. |
| `Alt+k` | Listar os nomes de teclas reconhecidos pelo mpv em um editor de texto. |

No menu de contexto, **View > More > Active Bindings In Text Editor** exporta
uma consulta dos bindings de runtime do mpv. Ela inclui o arquivo selecionado
por `--input-conf` e bindings de scripts, respeita prioridade e mantém `ignore`
visível para explicar teclas desativadas. É uma fotografia do momento da
consulta: scripts e overlays podem mudar os bindings disponíveis depois.
Os nomes do menu acompanham o idioma da interface. A consulta abre um arquivo
de diagnóstico; não grava nem substitui o `input.conf`.

Se uma tecla não funcionar:

1. Feche menus e janelas auxiliares e dê foco à janela principal. Em editores
   ou seletores, as teclas podem ser usadas pelo próprio controle. Atalhos
   locais dependem do foco; `global-input.conf` é um recurso separado.
2. Confira a tecla na lista ativa e procure substituições ou `ignore` no seu
   `input.conf`. Maiúsculas e minúsculas podem ter ações diferentes. Se houver
   bindings duplicados, a listagem de runtime reflete a resolução do mpv.
3. Compare com uma execução usando uma pasta vazia de configuração, sem apagar
   seu perfil. Em PowerShell, a partir da pasta do player:

   ```powershell
   $perfilTeste = Join-Path $env:TEMP ('mpvnet-input-' + [guid]::NewGuid().ToString('N'))
   New-Item -ItemType Directory -Path $perfilTeste | Out-Null
   .\mpvnet.exe --process-instance=multi "--config-dir=$perfilTeste" "C:\Videos\teste.mp4"
   ```

4. Para inspecionar a tecla recebida sem executar seu comando, repita a chamada
   acrescentando `--input-test=yes`. Use esse modo somente no diagnóstico e
   feche pela barra de título; ele deixa de executar os atalhos de reprodução.
5. Registre a tecla, o layout do teclado, o foco, a versão do player e o resultado
   com cada perfil. Inclua opções como `--input-conf` e scripts que mudem input.

Um menu completo personalizado pode não oferecer as ações listadas acima.
O editor mantém as configurações existentes; não restaure padrões por cima do
arquivo só para diagnosticar. Para manter o OSC visível após reiniciar, veja
[Visibilidade dos controles OSC](manual.md#visibilidade-dos-controles-osc).

Para mudanças amplas em atalhos, menu ou input, consulte também:

- `docs/developer/mpv-integration.md`
- `docs/developer/configuration.md`
- `docs/developer/architecture.md`


