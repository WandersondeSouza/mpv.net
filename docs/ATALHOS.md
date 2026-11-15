# Atalhos

As tabelas mostram os atalhos padrão com a janela principal em foco.
Personalizações, opções de inicialização e scripts podem mudar os atalhos ativos.

- [Personalizar ou desativar um atalho](#personalizar-ou-desativar-um-atalho)
- [Consultar os controles padrão](#controles-padrão-de-reprodução)
- [Diagnosticar um atalho](#consultar-e-diagnosticar-atalhos)

## Personalizar ou desativar um atalho

1. Abra **Configuração → Mostrar editor de atalhos** (`Ctrl+i`).
2. Localize a ação pelo nome ou pela tecla atual.
3. Clique na coluna **Entrada** e pressione a nova tecla ou combinação.
   No diálogo de captura, **Configuração** confirma a seleção e
   **Limpar lista** apaga a entrada daquela ação. Uma ação pode ter mais de
   um atalho em linhas separadas.
4. Feche o editor para salvar e reinicie o player. Se houver a mesma tecla
   associada a comandos diferentes, o editor indica o conflito e mantém a
   janela aberta para correção.

<details>
<summary>Editar input.conf e conferir a precedência dos atalhos</summary>

Abra **Configuração → Editar input.conf** (`k`) ou abra a pasta ativa
com `Ctrl+f`. Faça uma cópia do arquivo antes de editar e reinicie o
player após salvar. O [guia de configuração](CONFIGURACAO.md#arquivos-principais)
também indica o arquivo de atalhos globais e os demais arquivos do perfil.

Cada linha associa uma tecla a um comando. As linhas abaixo mudam o salto da
seta direita para 10 segundos e desativam apenas `p`.

```ini
Right seek 10
p ignore
```

Maiúsculas e minúsculas são distintas. Use `Alt+k` para consultar os
nomes de teclas disponíveis no seu layout. O parser normaliza os nomes dos
modificadores, como `ctrl+` para `Ctrl+`.

Sem menu completo, os atalhos definidos no arquivo, inclusive `ignore`,
têm prioridade sobre os padrões do fork. Em menus completos com a sintaxe
`#menu:`, edite a linha existente para preservar o rótulo. O frontend
preserva as ações essenciais **Abrir arquivos** e **Sobre**. Migrações de
comandos e links legados usam backup.

Com `--input-conf=...`, o mpv usa o arquivo explícito, enquanto o editor
continua ligado ao `input.conf` do perfil ativo. Scripts e OSC podem ter
prioridade nas respectivas áreas. Consulte os
[atalhos ativos](#consultar-e-diagnosticar-atalhos) e as
[regras de configuração](developer/configuration.md#inputconf).

</details>

## Controles padrão de reprodução

| Ação | Atalho padrão | Observações |
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

A busca depende da mídia. Em transmissões ao vivo, os saltos seguem os limites
da janela pesquisável.

## Playlist, faixas, legendas e repetição

| Ação | Atalho padrão | Observações |
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

Os atalhos abaixo valem fora dos controles que capturam o mouse. Sobre o OSC,
os botões, a barra de progresso e a roda seguem as ações do painel.
Scripts também podem definir áreas e atalhos próprios.

| Ação | Atalho padrão | Observações |
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

Use **Exibir → Progresso** para executar `show-progress` ou atribua
uma tecla a esse comando no editor.

## J/K/L como configuração opcional

Para voltar, pausar e avançar com três teclas próximas, revise os conflitos na
tabela abaixo e acrescente estas linhas ao `input.conf`.

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
| `k` | Editar `input.conf` como texto | Play/Pause. A edição continua no menu **Configuração**. |
| `l` | Loop A-B | Avançar 5 s. O loop A-B continua no menu **Ferramentas**. |
| `J`, `K` | Sem binding próprio | Permanecem sem binding próprio; não são aliases das minúsculas. |
| `L` | Repetir arquivo | Permanece igual. |

Os defaults nativos embutidos do mpv são desabilitados pelo frontend na
inicialização padrão; portanto não se deve inferir ações para `j`/`J`/`K` a
partir de outro player. Scripts, personalizações e opções explícitas ainda podem
atribuir essas teclas: confira a lista ativa antes de usar o exemplo.

Para desfazer, restaure apenas as linhas modificadas do seu arquivo e reinicie.
Em um perfil sem menu completo e sem bindings anteriores nessas teclas, basta
remover as três linhas acrescentadas.

## Consultar e diagnosticar atalhos

Com a janela principal focada, os padrões incluem:

| Tecla | Ação |
| --- | --- |
| `Del` | Alternar a visibilidade do OSC durante a sessão. |
| `F2` | Abrir o seletor de bindings do mpv. |
| `?` | Mostrar/ocultar a lista de atalhos ativos na tela. |
| `Ctrl+i` | Abrir o editor de atalhos. |
| `Alt+k` | Listar os nomes de teclas reconhecidos pelo mpv em um editor de texto. |

No menu de contexto, **Exibir → Mais → Atalhos ativos no editor de texto**
mostra os atalhos ativos no momento da consulta. A lista inclui as atribuições
de scripts e do arquivo selecionado com `--input-conf`, respeita a prioridade
entre eles e mostra entradas `ignore`. A consulta abre um arquivo de
diagnóstico separado do `input.conf`.

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

Para manutenção, consulte a [integração com mpv](developer/mpv-integration.md#input),
a [configuração interna de atalhos](developer/configuration.md#inputconf) e a
[arquitetura](developer/architecture.md).
