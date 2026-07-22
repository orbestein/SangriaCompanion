
## Ajustes da v2.3.2

- Controle da mini HUD de bosses movido para o rodapé da aba **Bosses**, após “Na tela”.
- Controle da mini HUD de eventos mantido na aba **Eventos**.
- Seção “Mini HUDs” removida das Configurações.
- Rodapé com versão e Discord removido das Configurações.
- Layout dos controles de bosses e configurações ajustado para evitar textos cortados e sobreposição.
- Botão “Restaurar padrões” integrado aos controles visuais.

# Sangria Companion 2.0.1 — estabilização modular

Esta é a primeira etapa da reconstrução modular.

## Correção crítica

O BepInEx rejeitava a versão anterior porque o atributo do plugin usava uma versão inválida (`1.2.0.3-emergency-launcher`). Agora o atributo usa SemVer válido:

`2.0.1`

## Preservado

- Dashboard personalizável
- Catálogo de bosses por atos, pesquisa, favoritos e alertas
- Eventos e horário do servidor
- Rastreador de estado/respawn e HUD compacta
- Rastreador experimental de bosses móveis
- Coleta, receitas, árvore de fabricação e HUD extra
- Leitura experimental do inventário
- Descoberta de receitas reais
- Tema Sangria Falls
- Bloqueio de entrada durante interação
- Notificações mesmo com painel fechado

## Nova proteção modular

Cada serviço roda isoladamente. Se inventário ou receitas falharem, o botão SC, bosses, eventos e notificações continuam funcionando.

## Compilar

Feche o V Rising e execute `COMPILAR_E_INSTALAR.bat`.

Depois confira `BepInEx/LogOutput.log`. Deve existir:

`Loading [Sangria Companion 2.0.1]`


## v2.3.2

- Restaurada a mini HUD de bosses favoritos, inclusive durante sincronização.
- Adicionada mini HUD de eventos com evento ativo e próximos horários.
- As duas mini HUDs podem ser ligadas ou desligadas nas Configurações.
- A mini HUD de eventos pode ser arrastada e fechada individualmente.
