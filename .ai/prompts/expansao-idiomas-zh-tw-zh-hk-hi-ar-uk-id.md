# Prompt para Codex — Expansão de idiomas
Este documento é um plano de implementação futuro, não uma implementação concluída.

## Objetivo
Adicionar suporte integral a zh-TW (chinês tradicional Taiwan), zh-HK (chinês tradicional Hong Kong), hi-IN (hindi Índia), ar-SA (árabe Arábia Saudita), uk-UA (ucraniano) e id-ID (indonésio). Inventariar primeiro: se algum já existir, melhorar sem duplicar. Manter todas as traduções anteriores, incluindo zh-CN.

## Requisitos globais
1. Ler as instruções atuais do repositório e localizar todos os recursos de interface, mecanismos de cultura, fallback, testes e pacote de distribuição.
2. Usar o inglês original como referência semântica. Reaproveitar zh-CN com OpenCC s2twp e s2hk como pré-processamento opcional offline, mas revisar regionalmente expressões técnicas, vocabulário e sintaxe; não copiar cegamente caracteres convertidos.
3. Traduzir de verdade, com terminologia consistente, todos os textos visíveis: menus, diálogos, avisos, ações, configurações, loading, atalhos, acessibilidade e mensagens de erro. Proibir lacunas, traduções automáticas sem revisão e TODOs como solução final. Marcas e identificadores técnicos podem permanecer no original.
4. Preservar integralmente chaves, placeholders, marcadores, formatação, plurais, tags, escapes, links, dados técnicos, formatos de tempo e número; não inserir segredos, não traduzir conteúdo fornecido pelo usuário e não mudar regras de reprodução, banco ou catálogos.
5. Seleção manual, detecção pelo sistema e persistência devem reconhecer todas as novas variantes. Mostrar nomes nativos: 繁體中文（台灣）, 繁體中文（香港）, हिन्दी, العربية, Українська e Bahasa Indonesia. Garantir fallback explícito; chinês genérico simplificado → zh-CN, tradicional genérico → zh-TW, Macau → zh-HK, Singapura → zh-CN. Fallback neutro inglês para outros casos sem tradução.
6. Árabe deve ser escrito corretamente e validar RTL, bidi e mistura de valores técnicos com texto. ar-SA representa uma variante árabe; usuários do Egito/EAU podem receber a interface com fallback de texto, mas as formatações regionais não devem ser falsamente substituídas. Garantir suporte tipográfico para CJK, devanágari, árabe e cirílico.
7. Para todos os idiomas, conferir tradução regional correta, truncamento, quebras de linha, foco/navegação, alternância em tempo de execução e retorno à configuração anterior.
8. Criar testes automatizados de cobertura total de chaves, placeholders, resolução de cultura, loading, persistência, fallback, build e embalagem. Registrar resultados reais e pendências de ambiente; não alegar validação visual ou publicação não realizada.
9. Executar por fases: inventário, zh-TW/zh-HK, hi-IN, ar-SA/RTL, uk-UA, id-ID, resolução/fallback, testes, documentação e relatório com arquivos alterados, quantidade de chaves por idioma e limitações. Não publicar na Store nem alterar branch principal sem obedecer às políticas do repositório.

## Instruções específicas deste projeto
Arquivos gettext .po em lang/po (por exemplo zh_CN.po); inspecione o loader NGettext, seleção de idiomas, compilação .mo, empacotamento e localização de catálogos. Criar zh_TW.po, zh_HK.po, hi_IN.po, ar_SA.po, uk_UA.po e id_ID.po, adaptando nomes se exigido pelo loader. Comparar msgid/msgctxt/msgid_plural, plurais, cabeçalhos PO, fuzzy, formato printf e configurações técnicas mpv; manter parâmetros, flags e atalhos. Executar msgfmt --check quando disponível. Testar menus, player, playlists, configurações, taskbar/SMTC, legendas, instalações portáteis e Microsoft Store. Atualizar .ai/prompts/README.md caso apropriado.
