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

## Agora

- Refinar README, roadmap e plano para refletirem o estado atual do fork.
- Usar `.ai/prompts/next-improvements.md` como prompt mestre para novas rodadas.
- Validar links locais da documentação após cada mudança.
- Separar pendências documentais de correções técnicas que exigem análise do código.

## Próximo

- Validar caminhos longos por Explorer, associação de arquivo e menu `Open Files...`.
- Validar visualmente a janela do editor de atalhos ao tentar salvar duplicidade.
- Validar visualmente thumbnails do `thumbfast` em uma UI compatível de uso real.
- Validar instalador, publicação GitHub e testes manuais completos de fullscreen, menu, atalhos, persistência de configuração e temas.

## Futuro

- Autoplay intermitente.
- Flash branco ao fechar.
- Black levels.
- OneDrive/cloud files.
- uosc.
- yt-dlp.
- LUT.
- AI upscaling.

## Critério para mover itens

Um item só deve sair de `Próximo` ou `Futuro` quando houver evidência no repositório ou validação local. Quando uma tarefa depender de comportamento do player, trate como mudança técnica separada e preserve compatibilidade com mpv/libmpv.
