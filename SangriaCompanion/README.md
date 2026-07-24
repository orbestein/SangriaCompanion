# Sangria Companion

## Versão atual: v2.6.1

Companion modular para o servidor Sangria de V Rising, com bosses, eventos, pets/almas da sessão, coleta, receitas, inventário experimental e HUDs configuráveis.

## Novidades da v2.6.1 — pets e almas separados por atos

- A tela **Pets e Almas** agora organiza os drops em grupos recolhíveis de **Ato 1**, **Ato 2**, **Ato 3** e **Ato 4**.
- Cada grupo mostra a quantidade de tipos diferentes e o total de drops daquele ato.
- O rodapé apresenta um resumo compacto com o total geral e os totais A1, A2, A3 e A4.
- O ato é identificado automaticamente quando o nome interno do item contém o nome do boss de origem.
- Itens personalizados podem ser classificados em `SessionDrops.ActMappings` usando o formato `nome=ato`.
- Itens ainda não reconhecidos continuam visíveis em **Sem ato definido**, evitando perda de registro.
- Os avisos de drop agora também informam o ato identificado.

### Mapeamento manual de atos

No arquivo de configuração do BepInEx, use:

```ini
[SessionDrops]
ActMappings = morgana=4,soulshardmonster=4,manticore=4
```

Outros exemplos:

```ini
ActMappings = lobo=1,necromante=2,aranha=3,morgana=4
```

## Novidades da v2.6.0 — pets e almas da sessão

- O Rastreador de bosses foi removido temporariamente da interface e da execução.
- A antiga aba **Rastreador** foi substituída por **Pets e Almas**.
- A primeira leitura do inventário cria uma linha de base: itens que já estavam na mochila não são contabilizados.
- A tela registra somente pets e almas obtidos depois dessa linha de base.
- Mover um item para um baú e devolvê-lo à mochila não conta como um novo drop.
- O botão **Limpar sessão** reinicia a contagem usando o inventário atual como nova linha de base.

### Reconhecimento inicial

O detector reconhece nomes internos contendo termos como:

- `SoulShard`, `Soul` ou `Alma`;
- `Pet`, `Familiar`, `Companion` ou `Summon`.

Palavras adicionais podem ser configuradas em `SessionDrops.AdditionalKeywords`.

## Novidades da v2.4.0 — sincronização global de bosses

- Substituídas consultas individuais `.boss tempo <boss>` por uma única consulta `.boss tempo`.
- A resposta geral atualiza o cache compartilhado por Dashboard, Bosses, favoritos, alertas e mini HUD.
- Atualizações automáticas reduzem spam no chat e carga no servidor.

## Recursos principais

- Dashboard personalizável.
- Catálogo de bosses por atos, pesquisa, favoritos e alertas.
- Mini HUD de bosses favoritos.
- Eventos, horário do servidor e mini HUD de eventos.
- Pets e almas obtidos durante a sessão, separados por atos.
- Coleta, receitas, árvore de fabricação e HUD compacta.
- Leitura experimental do inventário.
- Avisos configuráveis e silêncio temporário.
- Tema Sangria Falls e interface responsiva.

## Compilar e instalar

Feche o V Rising e execute:

```powershell
.\COMPILAR_E_INSTALAR.bat
```

O projeto procura automaticamente instalações comuns da Steam e copia `SangriaCompanion.dll` para `BepInEx/plugins` após a compilação.

## Atualizar o GitHub

```powershell
git status
git add .
git commit -m "v2.6.1 - separa pets e almas da sessão por atos"
git push origin main
git tag -a v2.6.1 -m "Sangria Companion v2.6.1"
git push origin v2.6.1
```
