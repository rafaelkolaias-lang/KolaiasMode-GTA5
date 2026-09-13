# KolaiasMode - GTA V Mod

Mod para GTA V com **menu GUI** que combina **Speed Limiter** + **Drift Handling** + **Population Density** em modulos independentes.

## Funcionalidades

### Menu GUI (F11)
- Menu visual no jogo com navegacao por teclado
- Cada modulo pode ser ativado/desativado separadamente
- Mostra status ON/OFF e detalhes de cada opcao

### 1. Drift + Speed Limiter
- **Speed Limiter**: limita a velocidade maxima do veiculo
  - Ao ativar, pega a velocidade atual como limite inicial
  - Ajuste com **Page Up / Page Down** (incremento de 5 km/h)
  - Segurar **Shift** desativa o limite temporariamente
- **Drift Handling**: modifica o comportamento do veiculo
  - Tracao reduzida para 65%
  - Aderencia reduzida para 25%
  - Freio reduzido para 25%
  - Angulo de curva reduzido para 50%
  - Segurar **Shift** restaura o freio original temporariamente
- Aplica automaticamente em qualquer veiculo que voce entrar
- Nao desativa ao sair do carro

### 2. Population Density
- Ajusta automaticamente a quantidade de NPCs e carros baseado na hora do jogo
- **Horario de pico** (7-10h e 17-20h): **200%** de densidade
- **Madrugada** (0-6h): **50%** de densidade
- **Resto do dia**: **100%** (normal)
- Aviso na tela quando muda o periodo

## Controles

| Tecla | Acao |
|---|---|
| **F11** | Abre / Fecha o menu GUI |
| **Seta Cima / Baixo** | Navega entre as opcoes no menu |
| **Enter** | Ativa / Desativa a opcao selecionada |
| **Esc** | Fecha o menu |
| **Page Up** | Aumenta limite de velocidade (+5 km/h) |
| **Page Down** | Diminui limite de velocidade (-5 km/h) |
| **Shift** (segurar) | Desativa limite de velocidade + restaura freio |

## Requisitos

1. **ScriptHookV** (Alexander Blade) - [dev-c.com/gtav/scripthookv](http://www.dev-c.com/gtav/scripthookv/)
2. **ScriptHookVDotNet** v3.7.0+ (nightly) - [github.com/scripthookvdotnet/scripthookvdotnet-nightly](https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases)

## Instalacao

1. Instale o **ScriptHookV**:
   - Extraia `ScriptHookV.dll`, `dinput8.dll` e `NativeTrainer.asi` na raiz do GTA V

2. Instale o **ScriptHookVDotNet nightly** (v3.7.0+):
   - Extraia `ScriptHookVDotNet.asi`, `ScriptHookVDotNet2.dll` e `ScriptHookVDotNet3.dll` na raiz do GTA V
   - **IMPORTANTE**: Use a versao **nightly**, nao a stable v3.6.0 (veja erros conhecidos abaixo)

3. Copie `ScriptHookVDotNet.ini` para a raiz do GTA V

4. Crie a pasta `scripts` na raiz do GTA V (se nao existir)

5. Copie `SpeedLimiter.cs` para a pasta `scripts`

### Estrutura de arquivos
```
Grand Theft Auto V/
├── dinput8.dll              (ScriptHookV)
├── ScriptHookV.dll          (ScriptHookV)
├── NativeTrainer.asi        (ScriptHookV)
├── ScriptHookVDotNet.asi    (SHVDN nightly)
├── ScriptHookVDotNet2.dll   (SHVDN nightly)
├── ScriptHookVDotNet3.dll   (SHVDN nightly)
├── ScriptHookVDotNet.ini    (config)
└── scripts/
    └── SpeedLimiter.cs      (este mod)
```

## Erros conhecidos e solucoes

### 1. `using GTA.UI` - namespace nao existe
**Erro**: `Uma diretiva de namespace using so pode ser aplicada a namespaces. 'GTA.UI' e um tipo, nao um namespace`

**Causa**: `GTA.UI` e uma classe, nao um namespace no SHVDN v2/v3. Usar `Notification.Show()` nao funciona.

**Solucao**: Usar natives diretas para notificacoes:
```csharp
// Em vez de: Notification.Show("texto");
Function.Call((Hash)0x202709F4C58A0424, "STRING");
Function.Call((Hash)0x6C188BE134E074AA, "texto");
Function.Call((Hash)0x2ED7843F8F801023, false, false);
```

### 2. ScriptHookVDotNet v3.6.0 (stable) - crash fatal `NativeMemory`
**Erro**: `System.TypeInitializationException: O inicializador de tipo de 'SHVDN.NativeMemory' acionou uma excecao`

**Causa**: A versao stable v3.6.0 do SHVDN nao e compativel com builds recentes do GTA V (ex: build 3889.0).

**Solucao**: Usar a versao **nightly** (v3.7.0+) do ScriptHookVDotNet:
```
https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases
```

### 3. `GET_VEHICLE_HANDLING_FLOAT` - native nao existe
**Erro**: `SCRIPT HOOK V CRITICAL ERROR - FATAL: Can't find native 0x642FC12F36B5E6DE`

**Causa**: As natives `GET_VEHICLE_HANDLING_FLOAT` (0x642FC12F36B5E6DE) e `SET_VEHICLE_HANDLING_FLOAT` (0x488C86D2B0C71BD1) nao existem em todas as builds do GTA V.

**Solucao**: Usar a API do SHVDN `veh.HandlingData` em vez de natives:
```csharp
// Em vez de: Function.Call(Hash.GET_VEHICLE_HANDLING_FLOAT, veh, "CHandlingData", "fBrakeForce");
var hd = veh.HandlingData;
float brake = hd.BrakeForce;
hd.BrakeForce = brake * 0.25f;
```

### 4. `DisableControlThisFrame` - numero errado de argumentos
**Erro**: Versoes diferentes do SHVDN esperam 1 ou 2 argumentos para `Game.DisableControlThisFrame()`.

**Solucao**: Usar a native direta que sempre aceita os mesmos parametros:
```csharp
// DISABLE_CONTROL_ACTION(group, control, disable)
Function.Call((Hash)0xFE99B66D079CF6BC, 0, 71, true);
```

### 5. `ConsoleKey=F4` - fecha o jogo
**Erro**: Pressionar F4 para abrir o console do SHVDN fecha o jogo (Alt+F4 / conflito).

**Solucao**: Desativar o console no `ScriptHookVDotNet.ini`:
```ini
ReloadKey=Insert
ConsoleKey=None
```

### 6. Script tenta exibir texto antes do jogo carregar
**Erro**: Crash ao chamar natives de texto no menu principal (antes de entrar no Story Mode).

**Solucao**: Verificar se o jogador esta no mundo antes de chamar qualquer native:
```csharp
if (Game.Player == null || Game.Player.Character == null)
    return;
if (!startupShown && Game.Player.CanControlCharacter)
{
    startupShown = true;
    ShowSubtitle("texto", 8000);
}
```

### 7. `SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME` - native nao existe
**Erro**: `SCRIPT HOOK V CRITICAL ERROR - FATAL: Can't find native 0x7A556D8427F8B1FC`

**Causa**: A native `SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME` nao existe em todas as builds do GTA V.

**Solucao**: Remover essa native e usar apenas as 4 que funcionam:
```csharp
// SET_PED_DENSITY_MULTIPLIER_THIS_FRAME
Function.Call((Hash)0x95E3D6257B166CF2, multiplier);
// SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME
Function.Call((Hash)0x245A6883D966D537, multiplier);
// SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME
Function.Call((Hash)0xB3B3359379FE77D3, multiplier);
// SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME
Function.Call((Hash)0xEAE6DCC7EEE3DB1D, multiplier);
```

## Valores do Drift Handling

| Parametro | Multiplicador | Efeito |
|---|---|---|
| TractionCurveMin | 0.65x | 35% menos tracao |
| TractionCurveMax | 0.65x | 35% menos tracao |
| TractionLossMultiplier | 0.25x | 75% menos aderencia |
| BrakeForce | 0.25x | 75% menos freio |
| SteeringLock | 0.5x | 50% menos angulo de curva |

## Population Density

| Horario do jogo | Densidade | Periodo |
|---|---|---|
| 7h-10h e 17h-20h | 200% | Horario de pico |
| 0h-6h | 50% | Madrugada |
| 6h-7h, 10h-17h, 20h-0h | 100% | Normal |

## Licenca

MIT License - use e modifique livremente.
