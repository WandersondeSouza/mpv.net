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

## Agora

- Refinar README, roadmap e plano para refletirem o estado atual do fork.
- Usar `.ai/prompts/next-improvements.md` como prompt mestre para novas rodadas.
- Validar links locais da documentação após cada mudança.
- Separar pendências documentais de correções técnicas que exigem análise do código.

## Próximo

- Validar build local em ambiente Windows completo.
- Validar pacote portátil.
- Verificar criação de `portable_config` no ZIP.
- Analisar `input.conf` e atalhos duplicados.
- Investigar long path.
- Investigar thumbfast na versão portátil.

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
