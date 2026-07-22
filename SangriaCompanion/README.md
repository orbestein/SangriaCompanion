
## Ajustes da v2.3.2

- Controle da mini HUD de bosses movido para o rodapé da aba **Bosses**, após “Na tela”.
- Controle da mini HUD de eventos mantido na aba **Eventos**.
- Seção “Mini HUDs” removida das Configurações.
- Rodapé com versão e Discord removido das Configurações.
- Layout dos controles de bosses e configurações ajustado para evitar textos cortados e sobreposição.
- Botão “Restaurar padrões” integrado aos controles visuais.

# Sangria Companion v1.2.0.2 — HUD Startup Fix

Correções desta versão:

- remove a dependência do patch Harmony em `ClientChatSystem.OnUpdate`;
- localiza o World do cliente de forma segura durante o `Update`;
- garante que uma falha em patch opcional não impeça a HUD de ser criada;
- mantém o botão `SC` visível mesmo se a configuração `Enabled` estiver inconsistente;
- preserva tema Sangria, notificações, coleta, receitas e rastreamento móvel experimental.

Feche o V Rising antes de executar `COMPILAR_E_INSTALAR.bat`.


## Versão 2.3.2

- Corrigido bloqueio de movimentação com HUDs abertas.
- Removido reset global dos eixos WASD.
- Adicionados controles globais e por categoria para avisos.
- Adicionado silêncio temporário por 1 hora ou até reiniciar.
- Adicionados tempos configuráveis de antecedência para eventos e bosses.
- Adicionada duração configurável para avisos de evento, boss e coleta.
- Adicionado botão para restaurar os padrões.


## v2.3.2

- Restaurada a mini HUD de bosses favoritos, inclusive durante sincronização.
- Adicionada mini HUD de eventos com evento ativo e próximos horários.
- As duas mini HUDs podem ser ligadas ou desligadas nas Configurações.
- A mini HUD de eventos pode ser arrastada e fechada individualmente.
