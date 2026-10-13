# Preenchedor de Cadastro · BR MED (protótipo desktop em C#)

O colaborador digita nome, e-mail, função, setor e CPF uma vez e cola tudo nos campos
de sistemas desktop do Windows com um atalho.

## Atalhos (funcionam em qualquer janela)

| Atalho | O que faz |
|---|---|
| Ctrl+Alt+F | Abre o formulário de dados (sempre o formulário principal) |
| Ctrl+Alt+V | Cola os dados na janela ativa, conforme o perfil daquele sistema |
| Ctrl+Alt+M | Mapeia o campo que está **sob o mouse** para receber um dado (colar) |
| Ctrl+Alt+K | Mapeia o campo que está **sob o mouse** para **capturar** o valor dele |
| Ctrl+Alt+C | Captura os campos mapeados da janela ativa e joga no formulário (sem salvar) |

O programa fica no ícone perto do relógio. Em **Configurações → Formulário principal** (menu do ícone) escolha qual
formulário o Ctrl+Alt+F sempre abre; o nome dele aparece no topo da janela. Pelo menu do ícone: formulário, meus formulários, sistemas mapeados,
sistema de teste, apagar dados e sair.

## Capturar dados de outro sistema ou site

1. Abra o sistema (ou o site no navegador), aponte o mouse para o campo de onde quer copiar e pressione **Ctrl+Alt+K**.
   Escolha em qual dado do formulário ele cai. Repita para cada campo; pode ser de vários sistemas e sites.
2. Nas próximas vezes, com a tela aberta, pressione **Ctrl+Alt+C**: os valores vão para o formulário, que abre
   com eles preenchidos. Complete o que faltar e clique em **Salvar dados** (ou não), depois use **Ctrl+Alt+V** nos destinos.
3. Campos mapeados para captura em outras telas do mesmo programa são ignorados. No navegador a captura depende de a
   página expor os campos à acessibilidade do Windows.

O formulário **não fecha ao salvar**; use **Limpar dados** para começar com outra informação.

## Formulários próprios

Além do "Cadastro padrão", cada colaborador pode criar quantos formulários quiser (menu do ícone → **Meus formulários…**
ou **Gerenciar formulários…**). Em cada um define os campos: rótulo, tipo (Texto, E-mail, Número ou Documento: CPF, RG ou Passaporte, que
validam o que for digitado; CPF e RG têm as opções "com pontos" e "sem pontos" ao mapear) e se é obrigatório. Os rótulos longos quebram linha.

- Cada sistema mapeado usa **um** formulário. Ao mapear um sistema novo com Ctrl+Alt+M, escolha de qual formulário vêm os dados;
  para mudar depois, use **Sistemas mapeados… → Trocar formulário…**.
- Só a estrutura dos formulários é salva (`formularios.json`, na mesma pasta dos perfis). Os dados digitados continuam só na memória.
- Excluir um formulário faz os sistemas que o usavam voltarem ao formulário padrão.

As janelas (formulário de dados, meus formulários, sistemas mapeados e sistema de teste) podem ser redimensionadas,
maximizadas e minimizadas.

## Gerar o executável (só quem desenvolve)

Opção mais simples, sem instalar nada: dê dois cliques em **`compilar.bat`**. Ele usa o compilador do
.NET Framework que já vem no Windows 10 e 11 e gera `saida\PreenchedorBRMED.exe`. Pode aparecer um aviso
dizendo que o compilador só suporta C# 5: é esperado e não impede a compilação.

Opção com Visual Studio: abra `PreenchedorBRMED.csproj` no Visual Studio 2022 (Community serve) e compile em Release.

Opção com o .NET SDK 8 instalado:

```
dotnet build -c Release
```

Pelo Visual Studio ou SDK, o resultado é **um único arquivo** em `bin\Release\net48\PreenchedorBRMED.exe`.
Ele usa o .NET Framework 4.8, que já vem no Windows 10 e 11, então o colaborador não instala nada.
Para abrir junto com o Windows, coloque um atalho do .exe na pasta `shell:startup`.

## Testar em 2 minutos

1. Rode o .exe e pressione **Ctrl+Alt+F**. Preencha e salve (CPF de teste válido: 529.982.247-25).
2. No menu do ícone, clique em **Abrir sistema de teste**. Clique em um campo dele e pressione **Ctrl+Alt+V**.
   Os 5 campos são preenchidos, incluindo a lista de departamentos.
3. Abra o **Bloco de Notas** e pressione **Ctrl+Alt+V**: os dados são digitados linha a linha
   (exemplo do método por digitação).
4. Para testar o mapeamento, aponte o mouse para um campo do sistema de teste, pressione **Ctrl+Alt+M**
   e escolha outro dado. Cole de novo para ver a mudança.

## Como funciona

Cada sistema tem um **perfil**, salvo em `%AppData%\BRMED\PreenchedorDeCadastro\perfis.json`.
O perfil é escolhido pelo nome do processo (ex.: `sistemarh` para `sistemarh.exe`) e, opcionalmente,
por um trecho do título da janela (`TituloContem`), útil quando o mesmo programa tem telas diferentes.

Há dois métodos:

**Automação (`"Metodo": "uia"`)**: cada campo é localizado pela UI Automation do Windows e recebe o valor
diretamente, sem depender de onde está o cursor. O mapeamento com Ctrl+Alt+M grava como reencontrar o campo,
nesta ordem de preferência: AutomationId, nome do controle e, em último caso, a posição entre os campos
do mesmo tipo. Listas (combobox) têm a opção correspondente selecionada.

**Digitação (`"Metodo": "teclado"`)**: plano B para sistemas que a automação não enxerga
(Java sem Access Bridge, Citrix, área de trabalho remota). O colaborador clica no primeiro campo e o programa
digita a sequência. Exemplo:

```json
{
  "Nome": "Sistema legado",
  "Processo": "legado",
  "Metodo": "teclado",
  "Sequencia": ["nome", "{TAB}", "email", "{TAB}", "{TAB}", "cpf_numeros", "{ENTER}"]
}
```

Passos aceitos: qualquer dado (`nome`, `email`, `funcao`, `setor`, `cpf`, `cpf_numeros`), `{TAB}`, `{ENTER}`,
`{ESPERA}` (pausa de 0,4 s) e `=texto` para digitar um texto fixo.

Para distribuir os mapeamentos, mapeie os sistemas em um computador e copie o `perfis.json`
para a mesma pasta nos outros.

## Privacidade (LGPD)

Os dados digitados ficam só na memória do programa e são apagados ao sair ou pelo menu "Apagar dados agora".
O `perfis.json` guarda apenas como encontrar os campos, nunca os dados do colaborador.

## Limitações conhecidas deste protótipo

- Sistemas abertos **como administrador** não aceitam automação de um programa comum.
  Se for o caso, troque `asInvoker` por `requireAdministrator` em `app.manifest`.
- Sistemas em **Java (Swing)** precisam do Java Access Bridge ativado para serem vistos; sem ele, use digitação.
- Em **Citrix/RDP** só a digitação funciona.
- Os atalhos são fixos no código (`AppContexto.cs`). Se algum conflitar com outro programa, o aviso inicial mostra qual.
- Antes de distribuir, **assine o .exe** (signtool) para evitar alertas do Windows e do antivírus.

## Estrutura

| Arquivo | Papel |
|---|---|
| `AppContexto.cs` | Ícone da bandeja, atalhos globais e orquestração |
| `FormDados.cs` | Formulário principal (monta os campos do formulário escolhido; validação de CPF e e-mail) |
| `Formularios.cs` / `FormModelos.cs` | Formulários criados pelo colaborador (JSON) e a tela para editá-los |
| `Controles.cs` | `RotuloQuebra` (rótulo que quebra linha) e `BotaoMarca` (botão no padrão do design system) |
| `Mapeador.cs` / `FormMapear.cs` | Identifica o campo sob o mouse e grava o mapeamento |
| `Preenchedor.cs` | Cola por UI Automation ou por digitação |
| `Teclado.cs` | Digitação simulada (Unicode, funciona com acentos) |
| `Perfis.cs` / `FormPerfis.cs` | Perfis salvos em JSON e tela de gerenciamento |
| `Aviso.cs` | Avisos no canto da tela que não roubam o foco |
| `SistemaTeste.cs` | Sistema falso para testar sem os sistemas reais |
| `compilar.bat` | Gera o .exe com o compilador que já vem no Windows |
| `Tema.cs` | Tokens do design system BR MED (cores, fontes Work Sans/Roboto se instaladas; senão Segoe UI) |
