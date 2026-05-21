# Roadmap do fork

Este roadmap organiza prioridades iniciais do fork em blocos pequenos. Ele deve ser revisado conforme testes reais forem concluidos.

## Concluido na base documental inicial

- README com identificacao do fork de manutencao.
- Documentacao de modo portatil.
- Documentacao de configuracao.
- Documentacao de atalhos e `input.conf`.
- Exemplos de `mpv.conf`, `input.conf` e `thumbfast.conf`.
- Templates de issue para bug e melhoria.
- Documentacao tecnica inicial em `docs/developer/`.
- Artefatos de IA em `.ai/`.
- Orientacao de uso do `thumbfast` no modo portatil.

## Concluido na estabilizacao pos-primeira versao

- Build local de `src\MpvNet.Windows\MpvNet.Windows.csproj` validado em Windows em 2026-05-21.
- Dry run local do pacote portatil validado com `src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -SkipGitHubRelease -SkipInstaller`.
- ZIP `mpv.net-v7.1.2.0-portable-x64.zip` inspecionado com `mpvnet.exe`, `libmpv-2.dll`, `MediaInfo.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`, `yt-dlp.exe`, `mpvnet.com`, `Locale/` e `portable_config/`.
- Smoke test do executavel extraido validado com video MP4 e imagem PNG gerados pelo `ffmpeg.exe` empacotado.
- `thumbfast.lua` real validado no modo portatil, carregando de `portable_config/scripts` com configuracao em `portable_config/script-opts`.
- Caminhos longos validados por terminal e instancia unica; arquivos locais com 260+ caracteres agora sao normalizados para `\\?\` antes do `loadfile`.
- Fluxo de aviso de atalhos duplicados validado por inspecao do codigo e build.
- Autoplay com `pause=yes` e playlist validado por IPC: sem `reset-on-next-file=pause`, o mpv mantem `pause=no` apos o usuario iniciar a reproducao; os exemplos agora documentam a opcao de reset.
- Checklist de release reorganizado para separar dry run validado, smoke test, compatibilidade, instalador/publicacao e itens fora de escopo.

## Pendente de validacao manual

- Validar caminhos longos por Explorer, associacao de arquivo e menu `Open Files...`.
- Validar visualmente a janela do editor de atalhos ao tentar salvar duplicidade.
- Validar visualmente thumbnails do `thumbfast` em uma UI compativel de uso real.
- Validar fullscreen, menu, atalhos, persistencia de configuracao e temas no ZIP extraido.
- Validar compatibilidade manual de `input.conf`, `mpv.conf`, `mpvnet.conf` e scripts no pacote extraido.
- Validar instalador, workflow manual, publicacao GitHub e links finais da release.
- Reproduzir visualmente flash branco ao fechar ou area fora do video cinza antes de qualquer correcao de UI.

## Agora

- Fechar pendencias manuais pequenas com evidencia objetiva.
- Manter `docs/release-checklist-ptbr.md` atualizado depois de cada validacao real.
- Corrigir apenas bugs tecnicos pequenos com reproducao, arquivo envolvido, risco e teste manual.
- Atualizar documentacao somente quando o comportamento real mudar ou quando uma pendencia for comprovadamente encerrada.

## Futuro

- Black levels.
- OneDrive/cloud files.
- uosc.
- yt-dlp avancado.
- LUT.
- AI upscaling.
- Recursos voltados a IPTV ou media center.

## Criterio para mover itens

Um item so deve sair de `Pendente de validacao manual` ou `Futuro` quando houver evidencia no repositorio ou validacao local. Quando uma tarefa depender de comportamento do player, trate como mudanca tecnica separada e preserve compatibilidade com mpv/libmpv.
