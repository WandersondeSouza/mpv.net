# Prompt Codex — Melhorias de integração SMTC com o Windows

## Missão

Trabalhe no repositório `WandersondeSouza/mpv.net` para melhorar a integração já existente do **MPV.NET Media Player** com os **System Media Transport Controls (SMTC)** do Windows.

O objetivo é deixar os controles do Windows mais responsivos e eficientes, publicar metadados e capas disponíveis com segurança, permitir busca apenas em mídias realmente pesquisáveis e manter a sessão controlável durante a reprodução em tela cheia.

Esta é uma melhoria incremental. A integração SMTC e a migração para .NET 10/Windows 19041 já existem no código atual. Não repita a migração de plataforma nem recrie o serviço SMTC. Confirme o estado real da branch antes de propor alterações.

## Decisão de comportamento em tela cheia

Para esta tarefa, **mantenha a sessão SMTC ativa durante fullscreen**. O objetivo é permitir que teclas multimídia, o painel de mídia do Windows e consumidores de sessões como o FluentFlyout continuem controlando o player.

Esta decisão atualiza especificamente o requisito do prompt histórico `.ai/prompts/windows-smtc-fluentflyout-net10-migration.md`, que pedia suspender a sessão em fullscreen. Não pare a reprodução ao entrar em fullscreen. Se algum controle exclusivo da taskbar precisar de tratamento diferente, separe-o da sessão SMTC.

Não adicione dependência direta do FluentFlyout, não copie código ou assets dele e não crie um widget próprio na taskbar.

## Leitura e diagnóstico obrigatórios

Antes de alterar arquivos:

1. Leia `AGENTS.md`, `.ai/README.md`, `.ai/skills/mpvnet-maintainer.md` e o agente aplicável em `.ai/agents/`.
2. Leia `README.md`, `docs/manual.md`, `docs/guia-operacional.md` e a documentação técnica atual de Windows UI, arquitetura e integração com mpv/libmpv.
3. Leia este prompt, o prompt histórico de migração SMTC e `src/MpvNet.Tests/MediaTransportTests.cs`.
4. Confirme o comportamento na branch atual. Classifique cada item desta tarefa como implementado, pendente, não validado ou fora do escopo. Prompts e relatórios antigos são contexto, não prova do estado atual.
5. Inspecione pelo menos:
   - `src/MpvNet.Windows/Services/MediaTransport/WindowsSystemMediaTransportService.cs`;
   - `src/MpvNet.Windows/Services/MediaTransport/MediaTransportController.cs`;
   - `src/MpvNet.Windows/Services/MediaTransport/MediaTransportModels.cs`;
   - `src/MpvNet.Windows/Services/MediaTransport/MediaTransportMetadataBuilder.cs`;
   - `src/MpvNet.Windows/WinForms/MainForm.MediaTransport.cs`;
   - `src/MpvNet.Windows/WinForms/MainForm.Fullscreen.cs`;
   - `src/MpvNet.Windows/Native/Taskbar.cs`;
   - `src/MpvNet.Tests/MediaTransportTests.cs`.
6. Consulte a documentação oficial atual da Microsoft para SMTC, timeline, thumbnails e comandos. Use FluentFlyout somente como consumidor externo para validar a sessão.
7. Registre antes de implementar:

```text
Resumo do entendimento atual:
Arquivos envolvidos:
Problema encontrado:
Mudança proposta:
Riscos:
Plano de teste:
```

## Escopo de implementação

### 1. Reduzir atualizações repetidas

Investigue o timer atual de atualização de mídia e todos os efeitos de cada publicação.

- Evite republish de metadados quando a mídia e os metadados não mudaram.
- Não execute `DisplayUpdater.ClearAll()` e `DisplayUpdater.Update()` em cada atualização de posição.
- Evite chamar a API de atualização dos botões da taskbar quando ícones, tooltips e habilitação não mudaram.
- Atualize metadados quando carregar/trocar mídia ou quando os campos relevantes mudarem.
- Atualize estado e timeline imediatamente após carregar mídia, Play/Pause, Stop, seek, mudança de playlist, fim de mídia e transição de fullscreen.
- Durante reprodução, atualize a timeline em baixa frequência, aproximadamente a cada 5 segundos, conforme a recomendação oficial; mantenha um único timer cancelável somente se os eventos existentes não forem suficientes.
- Pare o timer quando não houver mídia reproduzindo. Evite polling de 500 ms, timers duplicados, operações sobrepostas ou callbacks após shutdown.
- Preserve a precisão percebida pelo usuário no painel do Windows. Se a cadência escolhida diferir da recomendação oficial, explique a evidência e valide o comportamento real em Windows 10 e 11.

### 2. Completar metadados e usar capa disponível com segurança

O modelo atual contém `ThumbnailPath`, mas confirme se ele é preenchido e consumido na branch atual.

- Preserve título, artista, álbum e número da faixa já existentes.
- Acrescente artista do álbum (`AlbumArtist`) e outros campos somente quando o mpv disponibilizar valores confiáveis e o SMTC oferecer o campo correspondente.
- Use capa incorporada ou imagem local já disponível quando puder obtê-la sem bloquear a UI ou o player.
- Prefira APIs oficiais do Windows para publicar a miniatura. Valide a existência e o formato da imagem e libere streams/referências corretamente.
- Nunca faça download arbitrário apenas para preencher uma capa, não adicione serviço remoto ou dependência grande para extração, não exponha caminhos locais, credenciais ou tokens de URLs.
- Ao trocar para uma mídia sem capa, limpe a miniatura anterior.
- Falha ou ausência de capa deve manter os controles e a reprodução funcionando normalmente. Se a branch não tiver uma fonte segura de imagem sem ampliar muito o escopo, mantenha a capa opcional e registre essa limitação.

### 3. Habilitar busca somente quando a mídia for pesquisável

Adicione ao modelo de transporte uma capacidade explícita, como `CanSeek`, ou equivalente, se a auditoria confirmar que ela ainda não existe.

- Derive a capacidade do estado real do mpv, incluindo a propriedade `seekable` e duração finita e positiva.
- Não ofereça timeline pesquisável para stream ao vivo, mídia sem duração confiável ou mídia não pesquisável.
- Publique `MinSeekTime` e `MaxSeekTime` somente para um intervalo válido e pesquisável.
- Rejeite no controller pedidos de seek quando `CanSeek` for falso, mesmo que o evento tenha sido recebido pelo serviço Windows.
- Para seek aceito, valide posição negativa, posição acima da duração e valores inválidos antes de chamar libmpv.
- Mantenha o player como fonte autoritativa do estado. Atualize a timeline após o seek real, não apenas após receber o pedido do SMTC.

### 4. Avaliar avanço e retrocesso rápidos

Se as APIs SMTC disponíveis no TFM atual e o modelo do player permitirem uma integração simples:

- exponha Fast Forward e Rewind usando as propriedades e os botões SMTC correspondentes;
- habilite-os somente quando `CanSeek` estiver ativo;
- encaminhe-os para busca relativa, reutilizando o intervalo ou comportamento padrão já usado pelo MPV.NET;
- se não existir intervalo padronizado, use uma constante pequena e documentada, sem criar uma nova opção de configuração nesta tarefa;
- limite a posição ao começo e ao fim da mídia;
- não altere a taxa de reprodução para implementar saltos;
- se os botões não forem exibidos ou úteis nos alvos suportados, mantenha-os desabilitados e registre a evidência em vez de criar controles proprietários.

### 5. Manter a sessão ativa em fullscreen

- Remova a suspensão/limpeza automática da sessão SMTC causada apenas pela entrada em fullscreen.
- Preserve a sessão, metadados, timeline e comandos enquanto o player reproduz em fullscreen.
- Ao sair de fullscreen, atualize o estado atual sem duplicar eventos, handlers, timers ou sessões.
- Fechar o player em fullscreen deve continuar desabilitando a sessão e liberando recursos.
- Teste repetidas transições entre janela normal e fullscreen, tanto reproduzindo quanto pausado, e durante troca de mídia.
- Preserve o comportamento independente dos botões da taskbar quando necessário; não use a suspensão SMTC para ocultar um botão local.

### 6. Logs, falhas e segurança

- Mantenha a integração fail-open: indisponibilidade de SMTC nunca pode bloquear o player.
- Registre falhas e comandos externos relevantes usando o logger existente, sem registrar caminho completo, senha, token ou query string de URLs.
- Preserve a remoção de handlers e o descarte de timer, serviço, streams e referências Windows no ciclo de fechamento.
- Evite exceções de callbacks tardios depois de shutdown.
- Não faça refatoração ampla de libmpv, playlist, taskbar ou sistema de metadados além do necessário.

## Testes e validação

Amplie os testes em `src/MpvNet.Tests/MediaTransportTests.cs` ou nos arquivos de teste apropriados para cobrir:

- metadados iguais não causam publicação redundante;
- mudança de mídia atualiza metadados e limpa capa antiga;
- ausência ou falha de capa não derruba a sessão;
- AlbumArtist é publicado apenas quando disponível;
- mídia pesquisável habilita e aceita seek;
- mídia não pesquisável, stream sem duração e duração inválida desabilitam e rejeitam seek;
- posições negativas ou acima da duração são tratadas com segurança;
- Fast Forward/Rewind respeitam habilitação e limites, caso sejam implementados;
- fullscreen mantém a sessão e os comandos ativos;
- fechamento em fullscreen encerra a sessão sem callbacks remanescentes;
- botões da taskbar só são atualizados quando sua configuração muda.

Use o fluxo de validação definido pelo repositório:

```powershell
dotnet run --project src\MpvNet.Tests\MpvNet.Tests.csproj --no-restore
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj -c Debug
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj -c Release
```

Execute também `git diff --check`. Não use `dotnet test src/MpvNet.sln` como único teste; a solução inclui projeto WAP e o fluxo de testes está documentado em `.ai/README.md`.

Faça checklist manual em Windows 10 e Windows 11, quando os ambientes estiverem disponíveis:

- arquivo de áudio local com e sem tags/capa;
- vídeo local;
- URL/stream com credenciais, confirmando que nenhum segredo aparece no SMTC ou log;
- stream ao vivo ou não pesquisável;
- playlist no primeiro, meio e último item;
- controles Play/Pause/Stop/Previous/Next;
- seek, Fast Forward e Rewind, se implementados;
- teclas multimídia com a janela em segundo plano;
- painel de mídia do Windows e FluentFlyout quando instalado;
- entrada/saída repetida de fullscreen;
- fechamento do player durante reprodução e fullscreen.

Registre claramente o que foi validado localmente, validado manualmente, pendente de ambiente ou não verificado. Não declare FluentFlyout testado se ele não estava instalado.

## Restrições e autorização

- Preserve `net10.0-windows10.0.19041.0` e o suporte de plataforma atual.
- Não repita a migração .NET 10/19041 já consolidada.
- Não adicione dependência direta do FluentFlyout, não copie código/assets e não crie overlay próprio.
- Não altere configurações existentes, atalhos, comportamento da playlist, identidade ou versão pública do produto.
- Não atualize pacotes sem necessidade comprovada.
- Não crie branch, commit, push, release ou publicação automaticamente. Siga `AGENTS.md`, `.ai/README.md` e a solicitação atual do usuário.
- Não inclua segredos, binários, `bin/`, `obj/` ou alterações alheias.

## Critérios de aceite

- [ ] A sessão continua detectável e controlável pelo Windows durante fullscreen.
- [ ] O SMTC não recebe publicação de metadados ou botões sem mudança real.
- [ ] A timeline é atualizada em baixa frequência e imediatamente após eventos importantes.
- [ ] Seek só é oferecido e aceito quando a mídia é realmente pesquisável.
- [ ] Metadados e capa disponíveis são publicados com segurança e limpos ao trocar mídia.
- [ ] Falhas de thumbnail ou SMTC não afetam reprodução ou encerramento.
- [ ] Fast Forward/Rewind funcionam somente se suportados e respeitam limites.
- [ ] Handlers, timer e recursos Windows são liberados ao fechar o player.
- [ ] Testes automatizados relevantes passam e o diff está limpo.
- [ ] Validações manuais não são alegadas quando não puderam ser feitas.
- [ ] O comportamento do player sem FluentFlyout continua normal.

## Relatório final

Informe:

1. resumo do que já existia e foi confirmado;
2. arquivos alterados e mudanças feitas;
3. testes executados com comandos e resultados;
4. validações manuais feitas e ambiente;
5. o comportamento final em fullscreen;
6. se Fast Forward/Rewind foram implementados e como;
7. como a capa foi obtida e quais fallbacks existem;
8. limitações e validações que continuam pendentes.

Não encerre apenas com “implementado”; inclua evidências reproduzíveis.
