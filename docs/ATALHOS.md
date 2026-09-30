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

Para mudanças amplas em atalhos, menu ou input, consulte também:

- `docs/developer/mpv-integration.md`
- `docs/developer/configuration.md`
- `docs/developer/architecture.md`


