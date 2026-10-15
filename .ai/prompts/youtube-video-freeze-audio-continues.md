# Correção — vídeo congelado com áudio contínuo em mídia online

Trabalhe no repositório atual do MPV.NET Media Player. Corrija somente o caso em que uma mídia online continua tocando áudio enquanto a imagem permanece congelada, sem evento de fim de reprodução.

## Evidências iniciais

O usuário informou que o áudio continua enquanto a imagem congela. A captura de tela mostra o vídeo do YouTube “VÉIOS BAND Ao Vivo - Clássicos do Pop/Rock e Flashback 80 90 Show Completo - Parte 01” e o OSC exibe “Cache: 0s”.

O log fornecido foi:

C:\Users\Wanderson\AppData\Local\mpv.net\Logs\mpvnet-2026-10-14.log

Nesse registro:

- o item foi classificado como OnlineResolver e recebeu o perfil resilient, com cache=yes, cache-pause=yes, espera inicial de cinco segundos e limite de 256 MiB;
- às 20:18:39, o log registrou aproximadamente 9,3 segundos de cache e 100% de buffer;
- às 20:19:10 houve falhas curl ao receber/enviar dados; às 20:20:34 houve nova tentativa a partir do byte 20.971.520;
- às 20:26:27 o FFmpeg registrou repetidos erros de quadros VP9, incluindo cabeçalhos e marcadores inválidos, seguidos por “Error while decoding frame!”;
- o arquivo termina sem um evento end-file após esses erros de decodificação.

Trate isso como evidência inicial, não como causa definitivamente provada. Os erros indicam que o decodificador recebeu dados VP9 inválidos; as mensagens de transporte são compatíveis com interrupção, mas não provam se a origem está na conexão, CDN, representação selecionada, fonte ou integração mpv/FFmpeg. “Cache: 0s” é uma leitura do buffer do demuxer, arredondada para segundos pelo OSC; não prova, isoladamente, que a política de cache esteja desativada.

Confirme cada fato na branch e no código atuais antes de usá-lo. Se o log ou a imagem ainda estiverem acessíveis, reexamine-os sem expor URLs assinadas, parâmetros, cookies ou tokens.

## Escopo e fluxo de trabalho

1. Registre branch, HEAD e git status. Preserve alterações preexistentes; não descarte nem sobrescreva trabalho local.
2. Leia AGENTS.md, .ai/skills/mpvnet-maintainer.md, o agente de libmpv em .ai/agents/, README.md, docs/manual.md, docs/guia-operacional.md, docs/developer/mpv-integration.md e docs/logging.md.
3. Consulte .ai/prompts/playback-reliability-deep-audit.md e o trecho relevante de .ai/prompts/online-media-youtube-streaming-modernization.md para aproveitar a cobertura existente sem repetir trabalho amplo.
4. Inspecione o caminho real de mensagens do decoder, progresso de áudio/vídeo, cache, buffering, seek, troca de item e recuperação de erro. Verifique pelo menos Player.Events.cs, Player.ObservedProperties.cs, Player.NetworkCache.cs, Player.MediaLoading.cs, StreamingFailureDiagnostics.cs, LiveStreamRecoveryPolicy e seus chamadores, OSC e os testes relacionados.
5. Confirme na documentação/API da versão de mpv/libmpv usada pelo projeto quais propriedades ou eventos conseguem distinguir avanço de áudio de avanço de quadros de vídeo. Não invente uma propriedade nem use somente time-pos como prova de que o vídeo está avançando.
6. Antes da alteração, apresente entendimento atual, arquivos envolvidos, causa confirmada ou hipótese restante, mudança mínima, riscos e plano de validação. Em seguida, prossiga com a implementação autorizada por este prompt; não pare aguardando outra aprovação se a evidência permitir uma correção segura.

## Requisitos da correção

- Corrija o caso somente se a auditoria confirmar um comportamento do aplicativo que possa ser tratado com segurança.
- Erros isolados de decodificação não devem encerrar ou avançar a reprodução: o decoder pode se recuperar em um quadro posterior.
- Se necessário, reconheça uma falha persistente somente quando houver evidência combinada de erros de vídeo e ausência de avanço de quadros por um intervalo conservador, enquanto o áudio realmente progride.
- Não classifique como congelamento uma mídia pausada, em buffering, durante seek, com baixa taxa de quadros, imagem estática legítima, somente áudio, ou que voltou a apresentar quadros.
- Limite qualquer recuperação a uma ação por item e invalide temporizadores/estado ao trocar de item, pausar, buscar, iniciar buffering, recuperar quadros, encerrar ou fechar o player. Evite corridas com mudança de playlist e callbacks tardios.
- Preserve o player aberto. Não reconstrua nem recarregue a playlist e não reabra automaticamente a mesma URL em loop. Quando uma falha persistente estiver confirmada, siga a política existente do projeto e avance somente para o próximo item disponível. Sem próximo item, mantenha a janela aberta e use a apresentação de erro já existente, sem mensagens repetidas.
- Não altere por tentativa a qualidade do YouTube, seleção global de codec, política padrão de cache, opções explícitas do usuário, nem comportamento de arquivos locais ou de áudio-only.
- Não faça refatoração ampla nem duplicação dos mecanismos existentes de recuperação. Prefira uma política pura e testável, se isso se encaixar na arquitetura atual.
- Mantenha logs sanitizados: nunca grave URLs resolvidas/assinadas, cookies, tokens ou payloads sensíveis.
- Atualize documentação técnica existente somente se o comportamento realmente mudar e estiver consolidado.

## Testes e validação

Inspecione primeiro a cobertura existente em src/MpvNet.Tests, incluindo Program.cs quando aplicável e os testes de diagnóstico/recovery. Se a mudança criar comportamento novo, acrescente testes no padrão atual do projeto.

Cubra, conforme aplicável:

- erros VP9 repetidos com áudio avançando e vídeo parado por tempo suficiente: uma única recuperação;
- erro isolado seguido de novo quadro: nenhuma recuperação;
- pausa, buffering, seek, retomada, fechamento e troca de mídia: cancelar ou invalidar corretamente a detecção anterior;
- vídeo de baixa taxa de quadros ou imagem estática legítima: nenhum falso positivo;
- áudio-only, arquivo local e streams que não são OnlineResolver: nenhum novo efeito;
- item final da playlist e mudança concorrente da posição: manter a janela aberta e nunca avançar mais de um item.

Execute o harness e o build recomendados pelo repositório, de forma serial, registrando os resultados reais. Para mudança em libmpv/UI, faça também os testes manuais pertinentes da lista em AGENTS.md, com atenção a YouTube/URL, arquivo local, playlist, seek, pausa/retomada, fullscreen, alternância de faixa e fechamento. Se a fonte original não reproduzir de forma controlada, registre isso como validação manual pendente; não apresente testes unitários como prova do stream externo.

Não faça commit, push, publicação ou deploy sem pedido explícito.

## Critérios de aceite

- A causa do caso observado está separada entre evidência e hipótese.
- Erros transitórios de quadros não provocam avanço indevido.
- Uma falha persistente comprovada não deixa o aplicativo preso indefinidamente no item quando há próximo item.
- A recuperação mantém a janela aberta, não reconstrói nem recarrega a playlist e avança no máximo um item.
- Pausa, buffering, seek, áudio-only e mídias não relacionadas não geram falso positivo.
- Testes, build, revisão do diff e verificações manuais realizadas ou pendentes estão relatados com precisão.

