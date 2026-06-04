# Próximos Trabalhos

Documento curto para orientar a manutenção do fork `MPV.NET Media Player`.

Os itens aqui são pendências ou verificações em aberto. Quando algo for consolidado e validado, mova o histórico para `docs/changelog.md`.

## Foco imediato

- Reduzir responsabilidades de `src/MpvNet.Windows/WinForms/MainForm.cs` em etapas pequenas. Inventário inicial: janela principal, fullscreen, menu de contexto, encaminhamento de mensagens Win32, drag/drop, integração com `Player`, comandos de UI e eventos de mouse/teclado estão concentrados no mesmo arquivo.
- Reduzir responsabilidades de `src/MpvNet/Player.cs` em etapas pequenas. Inventário inicial: inicialização do mpv/libmpv, propriedades observadas, carregamento de mídia, playlist, títulos, MediaInfo opcional, comandos e encerramento do player estão concentrados no mesmo arquivo.
- Revisar `src/MpvNet.Windows/WPF/ConfWindow.xaml.cs` antes de novas mudanças de configuração, porque o arquivo concentra montagem da árvore, busca/filtro, edição e persistência visual do editor.

## Desenvolvimento prioritario

- Etapa segura recomendada: extrair helpers privados ou classes pequenas por tema, sem mover fluxo de UI/fullscreen/libmpv em bloco único.
- Antes de cada extração, proteger comportamento com build e validação do executável `src/MpvNet.Tests/MpvNet.Tests.csproj` quando a área tocar parser, paths, playlist, título, logs ou configuração.
- Checklist manual para mudanças de UI/libmpv: abrir arquivo local, URL/stream, playlist, pasta com mídia, drag/drop, menu de contexto, fullscreen, alternância de faixa/legenda, cursor/OSC, comandos de janela e fechamento do player.

## Itens já entendidos e não prioritários

- `src/MpvNet.Windows/WPF/HandyControl/` e `src/MpvNet.Windows/WPF/MsgBox/` incluem código de UI auxiliar/terceirizado ou derivado. Evitar reorganização ampla desses arquivos sem motivo funcional claro.

## Navegação técnica

Para cada item acima, a primeira leitura recomendada é:

- `docs/developer/architecture.md` quando o impacto for amplo;
- `docs/developer/configuration.md` quando a dúvida envolver inicialização ou configuração;
- `docs/developer/build-release.md` quando a dúvida envolver empacotamento, download ou release;
- `docs/developer/mpv-integration.md` quando a dúvida envolver `libmpv`, IPC com o player ou carregamento de mídia.
