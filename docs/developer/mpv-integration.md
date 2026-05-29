# Integração com mpv/libmpv

## Objetivo

Documentar conceitos relacionados à integração do mpv.net com mpv/libmpv.

---

# Visão geral

O mpv.net utiliza o mpv/libmpv como motor principal de reprodução multimídia.

A integração com libmpv é considerada uma das áreas mais críticas do projeto.

---

# Responsabilidades da integração

- reprodução de mídia;
- propriedades;
- comandos;
- sincronização;
- eventos;
- estado do player;
- integração com scripts.

---

# Wrapper e comunicação

Arquivos principais:

- `src/MpvNet/Native/LibMpv.cs` - P/Invoke para `libmpv-2.dll`;
- `src/MpvNet/MpvClient.cs` - wrapper de cliente, comandos, propriedades e eventos;
- `src/MpvNet/Player.cs` - inicialização, configuração inicial e estado do player.

## Ciclo de vida

Fluxo principal em `Player.Init`:

1. cria contexto com `mpv_create`;
2. registra eventos com `mpv_request_event`;
3. prepara propriedades iniciais;
4. define `config-dir` como `Player.ConfigFolder`;
5. carrega `input.conf` em memória quando existe conteúdo;
6. processa argumentos de linha de comando antes de `mpv_initialize`;
7. chama `mpv_initialize`;
8. cria cliente secundário `mpvnet`;
9. inicia o loop de eventos;
10. registra observadores de propriedades;
11. dispara o estado pronto para a UI.

## Eventos, propriedades e comandos

`MpvClient` traduz eventos nativos para eventos .NET e expõe callbacks para mudanças de propriedades, incluindo valores booleanos, inteiros, `double` e `string`.

Eventos importantes:

- `MPV_EVENT_SHUTDOWN`;
- `MPV_EVENT_LOG_MESSAGE`;
- `MPV_EVENT_CLIENT_MESSAGE`;
- `MPV_EVENT_END_FILE`;
- `MPV_EVENT_FILE_LOADED`;
- `MPV_EVENT_PROPERTY_CHANGE`;
- `MPV_EVENT_START_FILE`;
- `MPV_EVENT_PLAYBACK_RESTART`.

Comandos:

- `Command(string command)` para comandos textuais;
- `CommandV(params string[] args)` para comandos com argumentos separados.

Regra prática: quando o trabalho tocar eventos, propriedades, comandos ou `input.conf`, a investigação deve apontar a camada certa antes de alterar a implementação.

## Metadados auxiliares

A reprodução tem prioridade sobre metadados auxiliares. `MediaInfo.dll`,
listas de faixas, capítulos, duração, codec, idioma, título e propriedades
consultadas apenas para montar a interface não devem bloquear a tentativa de
reprodução quando o arquivo local existe ou a URL de streaming foi encaminhada
ao mpv. Falhas nessa coleta devem ser registradas como diagnóstico técnico e
tratadas com listas vazias ou valores padrão.

O mpv/libmpv é a autoridade final para decidir se a mídia abre. Validações do
frontend devem rejeitar apenas entradas claramente inválidas antes do `loadfile`;
ausência de legenda, áudio, duração, título ou dados de MediaInfo é falha não
bloqueante.

---

# Compatibilidade

O objetivo arquitetural é manter compatibilidade máxima com mpv.

Mudanças nessa camada devem considerar:

- scripts existentes;
- propriedades;
- linha de comando;
- comportamento esperado do mpv.

---

# Áreas críticas

## Eventos

Mudanças em eventos podem impactar:

- UI;
- scripts;
- sincronização;
- estado do player.

## Fullscreen

Pode depender da integração entre janela e libmpv.

## Input

Atalhos e comandos podem depender da integração com mpv.

---

# Recomendações para manutenção

1. Fazer mudanças pequenas.
2. Testar reprodução.
3. Validar scripts.
4. Validar fullscreen.
5. Validar comandos.
6. Validar propriedades.

---

# Melhorias futuras sugeridas

- documentação dos wrappers;
- detalhamento do fluxo de eventos;
- fluxo de propriedades;
- logs estruturados;
- diagnóstico avançado;
- ferramentas de debug.
