# Atalhos

Resumo dos arquivos de atalhos usados pelo mpv.net.

- `input.conf`: atalhos de teclado e mouse;
- `global-input.conf`: atalhos globais do Windows;
- `input-test` e `input-keylist`: modos de diagnóstico;
- o menu de contexto pode ser ajustado pelo `input.conf` compatível com o fork.

Use este arquivo como índice rápido. Para detalhes de configuração, veja [Configuração](CONFIGURACAO.md).

## Controles padrão de reprodução

- Clique esquerdo na área de vídeo: Play/Pause, quando houver mídia carregada.
- Duplo clique esquerdo: alternar tela cheia sem mudar Play/Pause.
- `p`, `P` e `Space`: Play/Pause.
- Botão do meio: Play/Pause; botão direito: menu de contexto.
- Roda: volume; botões laterais: anterior/próximo na playlist.

O clique simples aguarda somente o restante do intervalo de duplo clique do
Windows. Arrastar a janela, trocar a mídia ou fechar o player cancela a pausa
pendente. Cliques consumidos pelo OSC ou por scripts continuam sob controle do
mpv. A área reservada ao OSC pelo frontend não agenda pausa no vídeo.

`p` deixa de mostrar progresso no padrão. O comando `show-progress` continua
disponível no menu **View > Progress** (com o nome traduzido no idioma da
interface) e pode receber um atalho no editor de input.

Bindings explícitos em `input.conf`, inclusive `ignore`, têm prioridade.
Arquivos que já contêm um menu completo mantêm seus próprios bindings e não
recebem os novos padrões automaticamente. Nenhum arquivo do usuário é migrado
para impor esses controles.

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


