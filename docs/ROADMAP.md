# Roadmap do fork

Este roadmap organiza prioridades iniciais do fork em blocos pequenos. Ele deve ser revisado conforme testes reais forem concluídos.

## Concluído na base documental inicial

- README com identificação do fork de manutenção.
- Documentação de modo portátil.
- Documentação de configuração.
- Documentação de atalhos e `input.conf`.
- Exemplos de `mpv.conf`, `input.conf` e `thumbfast.conf`.
- Templates de issue para bug e melhoria.
- Documentação técnica inicial em `docs/developer/`.
- Artefatos de IA em `.ai/`.
- Orientação de uso do `thumbfast` no modo portátil.

## Concluído na estabilização pós-primeira versão

- Build local de `src\MpvNet.Windows\MpvNet.Windows.csproj` validado em Windows em 2026-05-21.
- Dry run local do pacote portátil validado com `src\Tools\release-mpv.net.ps1 .\src .\artifacts\release -SkipGitHubRelease -SkipInstaller`.
- ZIP `mpv.net-v7.1.2.0-portable-x64.zip` inspecionado com `mpvnet.exe`, `libmpv-2.dll`, `MediaInfo.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe`, `yt-dlp.exe`, `mpvnet.com`, `Locale/` e `portable_config/`.
- Smoke test do executável extraído validado com vídeo MP4 e imagem PNG gerados pelo `ffmpeg.exe` empacotado.
- `thumbfast.lua` real validado no modo portátil, carregando de `portable_config/scripts` com configuração em `portable_config/script-opts`.
- Caminhos longos validados por terminal e instância única; arquivos locais com 260+ caracteres agora são normalizados para `\\?\` antes do `loadfile`.
- Fluxo de aviso de atalhos duplicados validado por inspeção do código e build.
- Autoplay com `pause=yes` e playlist validado por IPC: sem `reset-on-next-file=pause`, o mpv mantém `pause=no` após o usuário iniciar a reprodução; os exemplos agora documentam a opção de reset.
- Checklist de release reorganizado para separar dry run validado, smoke test, compatibilidade, instalador/publicação e itens fora de escopo.

## Pendente de validação manual

- Validar caminhos longos por Explorer, associação de arquivo e menu `Open Files...`.
- Validar visualmente a janela do editor de atalhos ao tentar salvar duplicidade.
- Validar visualmente thumbnails do `thumbfast` em uma UI compatível de uso real.
- Validar fullscreen, menu, atalhos, persistência de configuração e temas no ZIP extraído.
- Validar compatibilidade manual de `input.conf`, `mpv.conf`, `mpvnet.conf` e scripts no pacote extraído.
- Validar instalador, workflow manual, publicação GitHub e links finais da release.
- Reproduzir visualmente flash branco ao fechar ou área fora do vídeo cinza antes de qualquer correção de UI.

## Agora

- Fechar pendências manuais pequenas com evidência objetiva.
- Manter `docs/release-checklist-ptbr.md` atualizado depois de cada validação real.
- Corrigir apenas bugs técnicos pequenos com reprodução, arquivo envolvido, risco e teste manual.
- Atualizar documentação somente quando o comportamento real mudar ou quando uma pendência for comprovadamente encerrada.

## Futuro

- Black levels.
- OneDrive/cloud files.
- uosc.
- yt-dlp avançado.
- LUT.
- AI upscaling.
- Recursos voltados a IPTV ou media center.

## Critério para mover itens

Um item só deve sair de `Pendente de validação manual` ou `Futuro` quando houver evidência no repositório ou validação local. Quando uma tarefa depender de comportamento do player, trate como mudança técnica separada e preserve compatibilidade com mpv/libmpv.
