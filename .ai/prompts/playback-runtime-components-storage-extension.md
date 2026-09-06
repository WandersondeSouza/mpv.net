# EXTENSÃO OBRIGATÓRIA — COMPONENTES DE RUNTIME, ATUALIZAÇÃO, CACHE, TEMP E CAMINHOS

Esta extensão complementa obrigatoriamente `.ai/prompts/playback-reliability-deep-audit.md`.

Ao executar a auditoria principal, tratar este arquivo como parte integrante do mesmo trabalho. Não executar tudo de uma vez: manter a mesma regra de etapas pequenas, testes, commit e push antes de avançar.

## OBJETIVO

Auditar e melhorar o ciclo completo dos componentes externos usados pelo MPV.NET Media Player, com foco em:

- libmpv normal;
- libmpv x86-64-v3;
- yt-dlp;
- ffmpeg;
- ffprobe;
- qualquer outro binário auxiliar atualmente baixado pelo projeto.

Mesmo que algum componente não seja estritamente necessário para a reprodução normal, NÃO removê-lo de imediato. Primeiro descobrir por que ele é distribuído, quais funcionalidades dependem dele e se a remoção produziria regressões. Se um componente estiver sendo baixado sem necessidade comprovada, documentar a conclusão e somente propor/remover em etapa separada, com testes.

---

# ETAPA A — INVENTÁRIO REAL DOS COMPONENTES

Antes de alterar código, localizar todo o fluxo relacionado a download, instalação, atualização, resolução de caminho, verificação de versão, hash, extração, limpeza e uso dos componentes externos.

Pesquisar no repositório por referências a:

- `libmpv-2.dll`;
- `libmpv-2-v3.dll`;
- `yt-dlp.exe`;
- `ffmpeg.exe`;
- `ffprobe.exe`;
- nomes de ZIPs/arquivos de distribuição;
- URLs de download;
- GitHub Releases/APIs utilizadas;
- `Process.Start` e criação de processos externos;
- componentes de bootstrap/runtime;
- diretórios de cache/temp;
- caminhos relativos ao executável;
- AppData/LocalAppData;
- APIs relacionadas a pacote/MSIX/Microsoft Store.

Para cada componente registrar:

1. origem oficial do download;
2. arquivo esperado;
3. caminho de instalação atual;
4. quem o utiliza;
5. se é obrigatório ou opcional;
6. como a versão atual é identificada;
7. como a versão disponível é identificada;
8. se existe hash/checksum/assinatura;
9. quando ocorre a verificação de atualização;
10. comportamento quando a rede está indisponível;
11. comportamento quando o arquivo local está corrompido;
12. comportamento quando o componente está em uso;
13. diferenças entre instalação normal, portátil e Microsoft Store.

Não alterar nada nesta etapa. Produzir baseline e testes quando possível.

---

# ETAPA B — PAPEL DE FFMPEG E FFPROBE

Investigar concretamente se `ffmpeg.exe` e `ffprobe.exe` externos são utilizados pelo aplicativo.

Diferenciar claramente:

- bibliotecas FFmpeg integradas/embutidas na build do mpv/libmpv;
- executáveis externos `ffmpeg.exe`/`ffprobe.exe`;
- utilização indireta pelo yt-dlp;
- utilização direta pelo MPV.NET.

Localizar chamadas diretas ou indiretas que dependam deles, incluindo download, merge de áudio/vídeo, pós-processamento, probing, thumbnails, conversão ou outra funcionalidade.

Se não existir dependência real do executável externo, NÃO concluir automaticamente que deve ser removido. Primeiro validar a integração do yt-dlp e cenários reais de URLs que possam requerer ffmpeg/ffprobe.

Resultado esperado: classificação de cada componente como `obrigatório`, `opcional`, `necessário apenas para determinadas funcionalidades` ou `aparentemente desnecessário`, acompanhada de evidência no código e teste.

---

# ETAPA C — RESOLUÇÃO CENTRALIZADA DE CAMINHOS

Os caminhos de componentes NÃO devem ficar espalhados pelo código.

Auditar se existe uma abstração central de runtime/components/storage. Se já existir e estiver correta, preservá-la e fortalecê-la. Não criar outra abstração concorrente.

Todas as áreas que utilizem libmpv, yt-dlp, ffmpeg, ffprobe, cache e arquivos temporários devem obter caminhos de uma fonte única/coerente.

Evitar:

- strings de caminho duplicadas;
- suposições de que o diretório atual é o diretório do aplicativo;
- `..` frágeis;
- gravação arbitrária ao lado do executável;
- dependência de working directory;
- caminhos específicos de uma máquina;
- hard-code de usuário;
- caminhos diferentes para download e posterior resolução do mesmo componente.

Criar testes unitários para a resolução de caminhos sempre que possível.

---

# ETAPA D — MODOS DE DISTRIBUIÇÃO

O MPV.NET deve funcionar corretamente nos seguintes cenários:

## 1. Instalação tradicional

Aplicativo instalado manualmente fora da Microsoft Store.

## 2. Portátil

Aplicativo executado sem instalação tradicional.

## 3. Microsoft Store / MSIX

Aplicativo distribuído pela Microsoft Store, onde o pacote de instalação pode ser somente leitura e as regras de armazenamento são diferentes.

NÃO assumir que o aplicativo Microsoft Store pode atualizar ou gravar componentes dentro do diretório instalado do pacote.

Detectar/usar corretamente o contexto de empacotamento já adotado pelo projeto.

A estratégia de diretórios deve respeitar permissões e isolamento do Windows.

Para instalação tradicional e portátil, investigar se realmente é seguro e desejável usar o mesmo diretório persistente de componentes. Se isso simplificar o projeto sem quebrar portabilidade, adotar uma política centralizada. Porém, não transformar uma versão portátil em instalação global inadvertidamente.

Para Microsoft Store, componentes baixáveis/atualizáveis devem ficar em local gravável apropriado da aplicação/usuário, nunca depender de modificar o pacote instalado.

Testar explicitamente os três modos ou, quando o ambiente não permitir teste real de Store, criar testes da lógica de resolução e documentar a validação manual necessária.

---

# ETAPA E — POLÍTICA DE DIRETÓRIOS

Definir claramente categorias distintas:

- diretório de componentes persistentes;
- diretório de downloads temporários;
- diretório de extração temporária;
- cache do mpv/demuxer quando aplicável;
- cache de shaders/GPU;
- cache ICC;
- cache/metadata do yt-dlp se existir;
- logs;
- configurações do usuário.

Não misturar arquivos permanentes e temporários.

Um componente validado e instalado NÃO deve ficar dentro de um diretório temporário que possa ser apagado pelo Windows.

Downloads parciais devem ficar em temp/staging e somente após validação serem promovidos para o diretório definitivo.

---

# ETAPA F — ATUALIZAÇÃO PERIÓDICA A CADA X DIAS

Auditar a política atual de atualização dos componentes.

Criar uma política configurável ou centralizada de intervalo de verificação. Não espalhar números mágicos pelo código.

A lógica esperada é:

1. componente não existe -> baixar imediatamente;
2. componente existe, mas está inválido/corrompido -> reparar imediatamente;
3. componente existe e está válido -> consultar atualização apenas quando o intervalo configurado tiver expirado;
4. se ainda não chegou o momento da consulta -> usar o componente existente sem acessar a rede;
5. se a consulta falhar por rede/servidor -> continuar usando a última versão válida;
6. se existir versão nova -> baixar para staging, validar e substituir de forma segura;
7. atualizar a data de última verificação somente de acordo com uma política coerente e testável.

Separar `última verificação` de `versão instalada`.

Não baixar novamente o mesmo arquivo toda vez que o programa iniciar.

Não consultar serviços remotos desnecessariamente em toda inicialização.

O valor de X dias deve possuir uma única fonte de configuração. Se o projeto já tiver intervalo definido, preservar o comportamento por padrão e apenas centralizar/corrigir se necessário.

Considerar atualização manual/forçada separadamente da verificação automática.

---

# ETAPA G — IDENTIFICAÇÃO DE VERSÃO

Para cada componente escolher a estratégia mais confiável disponível:

- versão publicada oficialmente;
- tag/release;
- hash do artefato;
- versão de arquivo;
- saída `--version` quando apropriado;
- metadata persistida após download.

Não usar apenas timestamp local do arquivo como prova de versão quando houver fonte melhor.

Para libmpv normal e x86-64-v3, garantir que as duas variantes pertençam a um conjunto compatível e não sejam atualizadas de forma que uma fique inconsistente com a outra.

---

# ETAPA H — INTEGRIDADE E SEGURANÇA DO DOWNLOAD

Auditar a cadeia completa de confiança.

Quando a fonte oficial fornecer SHA-256/checksum/assinatura confiável, validar antes da instalação.

No mínimo:

- baixar via HTTPS;
- usar fonte oficial/confiável;
- validar status HTTP;
- validar tamanho razoável;
- não tratar HTML/JSON de erro como executável/ZIP válido;
- validar formato do arquivo quando aplicável;
- impedir path traversal durante extração de ZIP;
- não executar arquivo parcial;
- não substituir componente válido antes da nova versão ser validada.

Nunca registrar tokens/credenciais em logs.

---

# ETAPA I — ATUALIZAÇÃO ATÔMICA E ROLLBACK

Nunca sobrescrever diretamente um componente que esteja sendo utilizado pelo player.

Fluxo recomendado:

1. verificar se atualização é necessária;
2. baixar para staging com nome temporário;
3. validar integridade;
4. validar conteúdo esperado;
5. opcionalmente executar diagnóstico seguro da nova versão;
6. garantir que o componente não esteja em uso ou programar aplicação segura da atualização;
7. substituir de maneira atômica quando possível;
8. preservar/fazer rollback para a versão anterior se a nova instalação falhar;
9. limpar staging após sucesso/falha.

Para libmpv, ter atenção especial: a DLL carregada pelo processo não deve ser trocada de maneira insegura enquanto está em uso.

Não tentar reinicializar libmpv arbitrariamente no meio de uma reprodução só para aplicar atualização.

---

# ETAPA J — LIMPEZA DE TEMP E CACHE

Criar/auditar política segura de limpeza.

Objetivos:

- remover downloads incompletos antigos;
- remover staging abandonado;
- remover arquivos temporários que sobraram após crash;
- controlar crescimento de cache;
- evitar acumulação indefinida de versões antigas;
- manter o componente atualmente válido;
- não apagar arquivo que esteja em uso;
- não apagar configuração do usuário;
- não apagar cache ativo durante reprodução.

A limpeza deve ser limitada SOMENTE a diretórios controlados pelo aplicativo.

Nunca executar limpeza recursiva perigosa baseada em caminho não validado.

Antes de apagar qualquer diretório, confirmar que o caminho resolvido pertence ao storage esperado do MPV.NET.

Preferir políticas como idade máxima, tamanho máximo ou limpeza de arquivos conhecidos em vez de `DeleteDirectory` indiscriminado.

Para cache do player, avaliar impacto na reprodução. Cache de reprodução em uso deve ser tratado separadamente de lixo de bootstrap/download.

Criar testes que garantam que arquivos fora da área gerenciada nunca são apagados.

---

# ETAPA K — MICROSOFT STORE: ATENÇÃO ESPECIAL

Esta etapa é obrigatória.

Auditar como a versão Microsoft Store resolve componentes externos e storage.

Verificar:

- pacote instalado somente leitura;
- diretórios graváveis disponíveis;
- persistência entre atualizações do aplicativo;
- comportamento após atualização da Store;
- permissões;
- path virtualization/redirection quando aplicável;
- conflito entre componente empacotado e componente baixado;
- migração de versões anteriores;
- limpeza sem tocar arquivos do pacote;
- startup quando não há rede;
- primeiro startup quando ainda não há componentes baixados.

A versão Store deve conseguir localizar sempre a mesma cópia válida do componente baixado, mesmo que o caminho físico do pacote da aplicação mude após uma atualização.

Não usar o diretório do executável como destino de componentes mutáveis na versão Store.

Se o projeto já possui uma solução correta, criar testes/documentação e preservar.

---

# ETAPA L — PORTÁTIL VS INSTALADO MANUALMENTE

Determinar explicitamente qual é a política desejada e qual é a política atual.

Se ambos puderem usar o mesmo mecanismo de componentes, centralizar a lógica.

Entretanto, respeitar expectativa de portabilidade:

- se modo portátil significa que tudo deve permanecer próximo ao aplicativo, preservar essa expectativa quando houver permissão;
- se o projeto já utiliza LocalAppData para componentes compartilhados, avaliar compatibilidade antes de mudar;
- não mudar silenciosamente o local das configurações do usuário;
- se houver migração de diretório, fazê-la de forma segura e compatível.

Não duplicar downloads simplesmente porque o executável foi iniciado de outro working directory.

---

# ETAPA M — YT-DLP E FFMPEG EM CONJUNTO

Garantir que o yt-dlp consiga localizar ffmpeg/ffprobe quando essas ferramentas forem necessárias.

Se os arquivos forem mantidos fora do diretório do yt-dlp, configurar explicitamente o caminho usando mecanismo suportado, em vez de depender acidentalmente do `PATH` global do Windows.

Não alterar globalmente o PATH do sistema.

Não depender de ffmpeg instalado pelo usuário na máquina para a funcionalidade que o MPV.NET promete fornecer com componentes próprios.

Ao mesmo tempo, URLs HTTP/HLS/DASH diretas que o libmpv consegue reproduzir não devem falhar porque yt-dlp, ffmpeg ou ffprobe não estejam disponíveis.

Criar testes/diagnósticos que distingam:

- falha do libmpv;
- falha do yt-dlp;
- ffmpeg ausente;
- ffprobe ausente;
- URL não suportada pelo yt-dlp;
- download/atualização de componente falhou.

---

# ETAPA N — STARTUP E DESEMPENHO

O processo de verificar componentes não deve tornar toda inicialização lenta.

Na inicialização:

- verificar existência/integridade local de forma barata;
- evitar chamadas de rede quando o intervalo ainda não venceu;
- quando possível, não bloquear a interface além do necessário;
- garantir que o componente obrigatório esteja pronto antes de inicializar o player;
- componentes opcionais não devem impedir reprodução local normal.

Não criar concorrência que permita duas atualizações simultâneas do mesmo componente.

Usar exclusão/sincronização apropriada para atualização, inclusive quando duas instâncias do aplicativo forem iniciadas próximas uma da outra.

---

# ETAPA O — TESTES OBRIGATÓRIOS

Adicionar testes para, no mínimo:

- resolução de caminho em modo normal;
- resolução de caminho portátil;
- resolução de caminho Store/empacotado abstraída;
- componente ausente;
- componente presente e válido;
- componente corrompido;
- intervalo de atualização ainda não vencido;
- intervalo vencido;
- servidor indisponível;
- nova versão disponível;
- mesma versão disponível;
- download parcial;
- checksum inválido;
- falha na substituição;
- rollback;
- limpeza de staging antigo;
- proteção para não apagar arquivos externos;
- duas solicitações simultâneas de atualização;
- yt-dlp sem ffmpeg;
- yt-dlp com ffmpeg encontrado pelo caminho configurado;
- libmpv normal/v3 no diretório resolvido.

Não depender de Internet real nos testes automatizados. Abstrair cliente HTTP/metadata quando necessário e usar arquivos/servidor local de teste.

---

# ETAPA P — LOGS E DIAGNÓSTICO

Logs úteis devem informar, sem dados sensíveis:

- modo da aplicação: Store/empacotado, instalado ou portátil;
- diretório de componentes resolvido;
- componente selecionado;
- versão instalada;
- data da última verificação;
- necessidade ou não de atualização;
- resultado da validação;
- fallback utilizado;
- falha de download;
- falha de integridade;
- limpeza executada e quantidade de arquivos removidos.

Não registrar URLs que contenham tokens/query strings sensíveis completas.

---

# POLÍTICA DE COMMITS DESTA EXTENSÃO

Executar cada etapa pequena separadamente.

Exemplos de commits:

```text
refactor(runtime): centralize component path resolution
test(runtime): cover packaged and portable paths
fix(runtime): harden component update staging
fix(runtime): validate downloaded component integrity
fix(runtime): preserve last valid component on update failure
fix(runtime): clean abandoned staging safely
fix(runtime): prevent concurrent component updates
fix(store): use writable component storage
fix(ytdlp): resolve bundled ffmpeg explicitly
```

Depois de CADA etapa:

```text
git status
dotnet build src\MpvNet.sln
dotnet test src\MpvNet.sln
git diff --check
git log -1
Não faça `git push` automaticamente. Só envie ao remoto mediante solicitação
explícita e após validar o estado do branch.
```

Não continuar se build/testes estiverem quebrados.

---

# RESULTADO FINAL OBRIGATÓRIO

No relatório final da auditoria principal, incluir uma seção específica `Runtime Components / Storage` contendo:

- inventário de componentes;
- quais são realmente necessários e por quê;
- papel de ffmpeg/ffprobe;
- caminho usado em instalação tradicional;
- caminho usado em modo portátil;
- caminho usado na Microsoft Store;
- política de atualização automática e valor de X dias encontrado/adotado;
- estratégia de versão/hash;
- estratégia de atualização atômica;
- comportamento offline;
- política de limpeza de temp/cache;
- riscos corrigidos;
- riscos ainda existentes;
- testes adicionados;
- commits correspondentes.

Mais importante que reorganizar diretórios é garantir que **cada modo de distribuição encontre sempre componentes válidos, atualizáveis e seguros sem depender de caminhos frágeis e sem quebrar a reprodução**.
