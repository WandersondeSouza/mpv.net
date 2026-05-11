# Skill: mpv.net Maintainer

## Objetivo

Ajudar a analisar, documentar, manter e melhorar o fork do mpv.net sem quebrar compatibilidade com mpv/libmpv, configurações existentes, scripts ou atalhos de usuário.

## Leitura inicial obrigatória

Antes de qualquer alteração, leia:

1. `AGENTS.md`;
2. `README.md`;
3. `docs/PLANO-CODEX.md`;
4. `docs/manual.md`;
5. documentação técnica relacionada à área tocada em `docs/developer/`;
6. arquivos diretamente relacionados ao problema.

Use estes documentos como fonte de contexto, mas valide sempre no código atual antes de afirmar comportamento.

## Prioridades

1. Preservar compatibilidade com mpv.
2. Entender o comportamento atual antes de editar.
3. Fazer mudanças pequenas e reversíveis.
4. Atualizar documentação quando comportamento mudar.
5. Separar hipóteses de fatos confirmados no código.
6. Evitar refatorações amplas sem pedido explícito.

## Mapa rápido de áreas

| Área | Documentação inicial | Arquivos principais |
| --- | --- | --- |
| Arquitetura | `docs/developer/architecture-ptbr.md` | `src/MpvNet.sln`, `src/MpvNet/`, `src/MpvNet.Windows/` |
| Inicialização | `docs/developer/startup-flow-ptbr.md` | `src/MpvNet.Windows/Program.cs`, `src/MpvNet/App.cs` |
| libmpv | `docs/developer/mpv-integration-ptbr.md`, `docs/developer/libmpv-wrapper-ptbr.md` | `src/MpvNet/Player.cs`, `src/MpvNet/MpvClient.cs`, `src/MpvNet/Native/LibMpv.cs` |
| Configuração | `docs/developer/configuration-system-ptbr.md`, `docs/CONFIGURACAO.md` | `src/MpvNet/App.cs`, `src/MpvNet/Player.cs`, `src/MpvNet/InputConf.cs`, `src/MpvNet/Settings.cs` |
| Comandos/atalhos | `docs/developer/commands-ptbr.md`, `docs/ATALHOS.md` | `src/MpvNet/Command.cs`, `src/MpvNet/InputConf.cs`, `src/MpvNet.Windows/GuiCommand.cs` |
| UI | `docs/developer/ui-ptbr.md` | `src/MpvNet.Windows/WinForms/`, `src/MpvNet.Windows/WPF/`, `src/MpvNet.Windows/UI/` |
| Build/release | `docs/BUILD.md`, `docs/release-checklist-ptbr.md` | `src/Tools/`, `src/Setup/`, `src/MpvNet.Windows/MpvNet.Windows.csproj` |

## Fluxo antes de editar

Produza este resumo antes de alterar código:

```text
Resumo do entendimento atual:

Arquivos envolvidos:

Problema encontrado:

Mudança proposta:

Riscos:

Plano de teste:
```

## Regras por tipo de mudança

### Integração com mpv/libmpv

- Trate como alto risco.
- Não altere nomes de comandos, propriedades ou opções sem migração documentada.
- Valide reprodução, eventos, scripts, fullscreen e encerramento.

### Configuração

- Preserve a ordem `MPVNET_HOME`, `portable_config`, `%APPDATA%\mpv.net`.
- Não mude sintaxe de `mpv.conf`, `mpvnet.conf` ou `input.conf` sem compatibilidade.
- Se qualquer migração alterar arquivo do usuário, criar backup e documentar.

### UI

- Validar tema claro/escuro, DPI, fullscreen, foco, menu de contexto e atalhos.
- Evitar mudanças visuais amplas junto com correção de lógica.

### Documentação

- Atualizar documentação operacional quando comportamento mudar.
- Separar documentação confirmada no código de pendências ou hipóteses.
- Preferir português brasileiro para documentação nova do fork, preservando documentos originais quando necessário.

## Validação recomendada

Use a validação mais estreita que cubra o risco:

- documentação: revisar links e coerência com o código citado;
- configuração: testar `MPVNET_HOME`, `portable_config` e `%APPDATA%`;
- input/comandos: validar `input.conf`, menu, atalhos padrão e atalhos customizados;
- UI: validar janela normal, fullscreen, tema claro/escuro e DPI;
- release: validar ZIP portátil, instalador e dependências nativas.
