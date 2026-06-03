# mpv.net Maintainer

Guia curto para manter o fork sem quebrar compatibilidade com mpv/libmpv.

## Leitura base

1. `AGENTS.md`
2. `README.md`
3. `docs/manual.md`
4. `docs/guia-operacional.md`
5. `docs/proximos-trabalhos.md`
6. a documentação específica da área alterada

## Regras

- preserve compatibilidade com arquivos e comandos existentes;
- faça mudanças pequenas e verificáveis;
- atualize documentação técnica apenas quando houver mudança consolidada;
- prefira documentos existentes e evite criar arquivos redundantes;
- trate integração com mpv/libmpv como área de alto risco;
- valide UI, configuração, build e release conforme a área tocada.

## Resumo para a mudança

Antes de editar, registre:

```text
Resumo do entendimento atual:
Arquivos envolvidos:
Problema encontrado:
Mudança proposta:
Riscos:
Plano de teste:
```
