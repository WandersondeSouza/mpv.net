# Agente: Configuração e atalhos do mpv.net

## Missão

Analisar e manter o sistema de configuração, modo portátil, `mpv.conf`, `mpvnet.conf`, `input.conf`, temas e hotkeys globais sem quebrar arquivos existentes de usuários.

## Ler primeiro

1. `AGENTS.md`;
2. `docs/CONFIGURACAO.md`;
3. `docs/ATALHOS.md`;
4. `docs/PORTATIL.md`;
5. `docs/developer/configuration-system-ptbr.md`;
6. `docs/developer/configuration-flow-ptbr.md`.

## Arquivos críticos

- `src/MpvNet/Player.cs`;
- `src/MpvNet/App.cs`;
- `src/MpvNet/InputConf.cs`;
- `src/MpvNet/Settings.cs`;
- `src/MpvNet.Windows/UI/Theme.cs`;
- `src/MpvNet.Windows/UI/GlobalHotkey.cs`.

## Regras

- Preservar a ordem de configuração: `MPVNET_HOME`, `portable_config`, `%APPDATA%\mpv.net`.
- Não normalizar ou reescrever arquivo do usuário sem necessidade.
- Criar backup antes de migração que altere arquivo do usuário.
- Validar atalhos duplicados sem quebrar compatibilidade com sintaxe do mpv.
- Documentar qualquer nova opção ou mudança de fallback.

## Testes manuais esperados

- Executar com `portable_config` ao lado do `mpvnet.exe`.
- Executar sem `portable_config`.
- Executar com `MPVNET_HOME`.
- Testar `input.conf` padrão e customizado.
- Testar menu de contexto quando `input.conf` contém sintaxe de menu.
