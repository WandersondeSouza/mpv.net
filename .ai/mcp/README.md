# MCPs úteis para manutenção do mpv.net

Este documento recomenda servidores MCP úteis para trabalhar neste fork. Ele não ativa nada sozinho: serve como guia para configurar o ambiente do agente quando necessário.

## Princípio

Use MCPs sob demanda. Para este projeto, eles devem ajudar a ler documentação, consultar repositório, investigar dependências e navegar no sistema de arquivos sem substituir a validação no código local.

## Recomendações

| MCP | Utilidade | Quando usar |
| --- | --- | --- |
| Filesystem | Acesso controlado aos arquivos do repositório | Leitura, auditoria e edição local assistida. |
| GitHub | Issues, PRs, releases e comparação com upstream | Triagem de bugs, histórico, PRs e releases. |
| Microsoft Learn | Documentação oficial .NET, Windows, WPF, WinForms, MSBuild | Dúvidas de plataforma e build. |
| NuGet | Consulta de pacotes e versões | Atualização ou auditoria de dependências. |
| Fetch/Web | Páginas públicas como manual do mpv, libmpv e documentação externa | Apenas quando a informação puder ter mudado ou não estiver no repo. |

## Escopo recomendado

Para Filesystem, limitar ao repositório deste fork:

```text
C:\Users\Wanderson\OneDrive\Documentos\MeusProjetos\WandersondeSouza\mpv.net
```

Evite dar acesso amplo ao usuário inteiro sem necessidade.

## Estado local esperado no Codex

No Codex App, confirme que `C:\Users\Wanderson\.codex\config.toml` inclui este repositório nos `args` de `[mcp_servers.filesystem]`. Sem esse caminho, o MCP filesystem pode funcionar para outros projetos, mas não ajuda diretamente na leitura assistida deste fork.

## Exemplo de configuração conceitual

Adapte ao formato do cliente MCP usado. Não coloque tokens diretamente no arquivo versionado.

```toml
[mcp_servers.filesystem]
command = "npx"
args = [
  "-y",
  "@modelcontextprotocol/server-filesystem",
  "C:\\Users\\Wanderson\\OneDrive\\Documentos\\MeusProjetos\\WandersondeSouza\\mpv.net"
]

[mcp_servers.github]
url = "https://api.githubcopilot.com/mcp/"
bearer_token_env_var = "GITHUB_MCP_PAT"

[mcp_servers.microsoft_learn]
url = "https://learn.microsoft.com/api/mcp"

[mcp_servers.nuget]
command = "dnx"
args = [ "NuGet.Mcp.Server", "--source", "https://api.nuget.org/v3/index.json", "--yes" ]
```

## Cuidados

- Não versionar tokens.
- Não usar MCP para aplicar mudanças sem revisar diff local.
- Não consultar internet para comportamento já definido no código local.
- Para documentação oficial do mpv/libmpv, preferir fonte oficial e registrar link usado no relatório.
- Em mudanças de build Windows, confirmar também no ambiente local.
