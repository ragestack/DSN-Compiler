# DSN Compiler

Автор и правообладатель: Леонов Денис <466611@mail.ru>

[Русский](#русский) · [English](#english)

## Русский

DSN Compiler – приложение для Windows для просмотра, исполнения и компиляции программ в форме функциональных логических схем OrCAD Capture DSN 2.0 (OrCAD Schematic, OrCAD 10). Позволяет выполнять программу, заданную в форме функциональной логической схемы формата DSN по шагам, наблюдать значения переменных, строить диаграммы состояний и сохранять автономные HTML-отчёты. Поддерживает библиотеки функций для различных архитектур процессоров: ADSP-2181, RISC-V RV32I и MSP430, а также сборку исполняемых бинарных образов средствами выбранного процессора. Приложение поддерживает пошаговое исполнение, анализ переменных и регистров, формирование HTML-отчётов и подготовку машинного кода для ADSP-2181, RISC-V RV32I и MSP430.

Поддерживаются одна страница и 32 типа функций. Блоки являются операциями программы, связи передают данные. Результат исполнения – значения переменных, история состояний и диаграммы по логическим тактам.

Алгоритм программы задаётся DSN-файлом в форме функциональной логической схемы. Ассемблерные реализации используемых функций и описания их интерфейсов находятся в `INC/ADSP21.INC`, `INC/RV32I.INC` и `INC/MSP430.INC`. Приложение исполняет используемое подмножество инструкций этих библиотек. Машинная сборка формирует отдельный модуль и образ выбранного ЦП; интеграция с устройством использует его карту памяти и окружение. В интерфейсе исполнения время измеряется логическими тактами.

Библиотеки подключаются независимо. В `implementations/*.json` общий ключ `LOGIC` сопоставляется конкретному INC выбранной архитектуры. Интерфейс показывает доступные реализации; при отсутствии ADSP21.INC можно использовать RV32I/MSP430. Первый доступный выбор: ADSP21, затем RV32I, затем MSP430. Для работы нужна хотя бы одна пара INC и описания реализации. Выбор явно задаётся в интерфейсе или через `--implementation`; сравнение использует доступные реализации.

### Лицензия

Перед самостоятельной сборкой или запуском ознакомьтесь с [лицензионным соглашением RU/EN](LICENSE.txt). Учебное использование и использование по судебному поручению бесплатны в пределах соглашения. Иное служебное и коммерческое использование требует отдельной платной лицензии. Распространение изменённых исполняемых сборок DSN Compiler – с предварительного письменного согласия правообладателя.

### Сборка на Windows 10 x64

Нужны штатный 64-разрядный Windows PowerShell 5.1 и примерно 3 ГБ свободного места. Проверяемая база Windows – сборка 19044. Распакуйте исходники в короткий доступный для записи путь, например `C:\DSN-src`. Полный автономный архив включает `dependencies/dotnet-sdk-10.0.404-win-x64.zip`; для исходного дерева этот архив нужно положить туда отдельно. Его версия, официальный URL и SHA-512 закреплены в `toolchain.lock.json`. Сборщик не скачивает файлы и сверяет SDK перед распаковкой.

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\Build.ps1
```

Установленные SDK, Git, Visual Studio, PowerShell 7, интернет и права администратора не требуются. Все инструменты, кэши и результаты располагаются внутри комплекта. Системные PATH, политика выполнения и службы не изменяются. Логотип и иконка создаются из предоставленного SVG. Результат – папка и ZIP переносимого приложения; пути и SHA-256 архива записаны в `release/latest.json`. Запускайте `DSNViewer.exe` из всей распакованной папки. Для запуска готового приложения исходники и SDK не нужны.

Параметр `-WorkDirectory` размещает инструменты, кэши и промежуточные файлы в отдельной папке. `-SdkDirectory` позволяет явно выбрать закреплённый SDK, `-ReleaseDirectory` задаёт папку выпуска. `-CompileOnly` выполняет компиляцию без формирования переносимого комплекта.

### Работа

1. Откройте собственный DSN и выберите одну из доступных архитектур.
2. Во вкладке «Данные / Параметры» задайте недостающие числа и окружение, нажмите
   «Проверить», затем «Подготовить». Числовые литералы DSN используются автоматически.
   Поддерживаются десятичные числа, `0xFFFF` и `B#1010`.
3. «Случайные тестовые данные» заполняет только пропуски по seed и диапазонам.
   Это тестовые предложения; подготовку и исполнение запускает пользователь.
4. «Шаг», «10 шагов», «Пуск/Пауза» выполняют логические такты. Порядок задаётся
   полными обозначениями блоков с Ordinal-сравнением: DD10 раньше DD2.
   Переименование блоков может изменить вычисления; записи памяти видны сразу.
5. Наблюдайте переменные, историю и диаграммы. Остановки и ожидаемые значения
   проверяются после завершённого такта. Просмотр прошлого не откатывает машину.
6. Данные сохраняют подготовку; сценарий повторяет опыт с начала; сеанс сохраняет
   память, регистры, историю и очередь и открывается на паузе. Для продолжения
   требуются тот же DSN и соответствующая версия библиотеки/контекста.

RU/EN переключаются без сброса сеанса. Настройка языка хранится в `settings/ui.json`; данные пользователя – в `contexts`, `scenarios`, `sessions` и `results` рядом с EXE.

```powershell
.\DSNRunner.exe --help --language ru
.\DSNRunner.exe --analyze-data программа.DSN анализ.json
.\DSNRunner.exe --run-user программа.DSN результат.json данные.json --steps 10 --implementation RV32I
.\DSNRunner.exe --scenario программа.DSN сценарий.json результат.json
.\DSNRunner.exe --resume программа.DSN сеанс.dsnsession 10 результат.json
```

Сохранения с подготовленным контекстом самодостаточны по данным подготовки. Внешний контекст исполнения при необходимости передаётся явно; он не выбирается по имени программы. После изменения INC его нужно явно перечитать и проверить; ошибочное сохранение не заменяет действующий сеанс.

### Машинная сборка

В окне исполнения нажмите «Машинный код…». Выберите исходный ассемблер, объектный модуль или связанный образ, папку средств ЦП и новую папку результата. Сборка использует выбранный INC и подготовленный контекст, включая начальную память. Один вызов `dsn_step` соответствует логическому такту. Сеанс остаётся на паузе; полученный код сохраняется отдельно. Пути средств ЦП запоминаются в локальном `settings/native-tools.json`.

Подготовьте данные тем же способом, что для исполнения. Отдельно распакуйте GNU RISC-V Embedded binutils для Windows (проверяемая база – xPack GCC 15.2.0-1). Передайте папку `bin` явно; приложение использует `as`, `ld`, `readelf` и `objdump` по указанному пути. Обычная сборка приложения использует закреплённый .NET SDK; средства ЦП подключаются для машинного экспорта.

```powershell
.\DSNRunner.exe --build-native программа.DSN результат C:\CPU-tools\bin данные.json --implementation RV32I
```

Результат в новой папке: `program.o`, `program.elf`, исходный ассемблер, карта памяти, дизассемблирование и `native-build.json` с версиями и SHA-256. Для отдельного объекта добавьте `--object-only`; линкер при этом не вызывается. Цель – RV32I / ILP32, память образа от `0x80000000`, 16 МиБ, стек 4 КиБ. Образ запускает `dsn_init` и затем вызывает `dsn_step` в цикле; модуль предоставляет эти две функции для включения в собственную прошивку. Таблица `Words` связывает переменные с экспортированными символами. Слова имеют размер 16 бит. `dsn_status=0` – обычное исполнение; `1` – остановка на адресе памяти.

Для MSP430 укажите `--implementation MSP430` и папку `bin` TI MSP430 GCC 9.3.1.11 (binutils 2.34). Цель – классическое ядро MSP430, малые модели кода и данных, размещение MSP430F1611: RAM `0x1100` / 10 КиБ, FLASH `0x4000` / 48 КиБ с областью векторов, стек 512 байт. Выход – те же `.o` и ELF32; 16-разрядная разрядность данных обозначена атрибутами MSPABI. Стартовый образ останавливает watchdog F1611; модуль `program.o` предоставляет точки вызова. Длительность логического такта задаёт вызывающее окружение.

Для ADSP укажите `--implementation ADSP21` и корневую папку VisualDSP++ 3.5 с `easm218x.exe`, `linker.exe`, `elfdump.exe`. Средства ADI используют действующую лицензию, включая пробную Test Drive. Выход – `program.doj` и `program.dxe`: ELF32 ADI, EM_ADSP=30, типы DOJ=7 и DXE=8, инструкции ADSP-2181 по 24 бита. Данные DM размещаются от 0, код PM от `0x30`; reset-вход находится в PM 0. Для вызова модуля задайте `MSTAT=0`, последовательные вызовы и эту карту памяти. Обёртка обслуживает линейную адресацию I3 и сохраняет L3 вызывающей стороны.

### Подготовка средств сборки образов

В папке готового приложения, рядом с DSNViewer.exe, запустите:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Setup-NativeTools.ps1
```

Сценарий подготовит RV32I и MSP430: получит официальные ZIP закреплённых версий, сверит SHA-256, извлечёт ассемблер, линковщик, readelf/objdump, нужные DLL и лицензии. Затем соберёт и прочитает контрольный ELF, сохранит пути в `settings/native-tools.json`. Откройте «Машинный код…»; средства уже подключены. Для исходного комплекта сначала выполните Build.ps1 и запустите Setup-NativeTools из созданной папки приложения. Для одной архитектуры добавьте `-Architecture RV32I` или `-Architecture MSP430`; язык сообщений выбирает `-Language ru` или `-Language en`.

По умолчанию используются `.tools/native` и `.cache/native-downloads` рядом со сценарием. Доступная для записи папка задаётся через `-ToolsRoot`. Системные PATH, службы и политика выполнения сохраняются. Повторный запуск проверяет имеющиеся файлы и повторяет контрольную сборку; настройка ADSP и пути другой архитектуры сохраняются. Результат и версии средств записаны в `settings/native-tools-setup.json`, журналы сборки – в `checks` внутри комплекта средств. Открытый сеанс приложения сохраняет свою выбранную библиотеку.

Для автономной подготовки заранее получите следующие официальные архивы:

- [xPack RISC-V 15.2.0-1](https://github.com/xpack-dev-tools/riscv-none-elf-gcc-xpack/releases/tag/v15.2.0-1):
  `xpack-riscv-none-elf-gcc-15.2.0-1-win32-x64.zip`;
- [TI MSP430 GCC](https://www.ti.com/tool/MSP430-GCC-OPENSOURCE):
  `msp430-gcc-9.3.1.11_win32.zip` (выпуск пакета TI 9.3.1.2).

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Setup-NativeTools.ps1 -ArchiveDirectory C:\DSN-archives
```

SHA-256 и точные URL закреплены в самом сценарии. Этот режим использует только указанную папку архивов. Папка уже подключённой версии проверяется по манифесту; для новой подготовки используйте другую папку `-ToolsRoot`.

### Подключение ADSP-2181

1. Получите [VisualDSP++ 3.5 for 16-bit Processors у Analog Devices](https://www.analog.com/en/resources/evaluation-hardware-and-software/software/vdsp-pp-sgp.html).
   Используется вариант для ADSP-21xx и процессор ADSP-2181.
2. Выполните штатную установку. В корневой папке должны находиться
   `easm218x.exe`, `linker.exe`, `elfdump.exe`; сохраните установленные
   библиотеки и служебные компоненты ADI.
3. В VisualDSP++ откройте **Help → About VisualDSP++ → Licenses → New**,
   подключите собственную лицензию либо Test Drive по
   [инструкции ADI](https://www.analog.com/media/en/technical-documentation/licenses/License_Installation_Procedure_v9.pdf).
   Test Drive рассчитан на 90 дней; постоянная работа использует действующую
   лицензию пользователя. Лицензия подготавливается на его компьютере.
4. В DSN Compiler выберите ADSP21, подготовьте данные, откройте «Машинный код…»,
   выберите «Исполняемый образ» и укажите корневую папку VisualDSP++ 3.5.
   Приложение запомнит путь. Результат – `program.dxe` и модуль `program.doj`.

Средства сборки для RV32I, MSP430 и ADSP подключаются отдельно и используются для получения машинных модулей и образов. Средства ЦП подключаются по необходимости; обычная сборка и исполнение приложения используют свой автономный комплект.

### Автономный отчёт

«Отчёт о выполнении…» приостанавливает программу и сохраняет выбранный диапазон завершённых тактов в один автономный HTML: сведения, полная функциональная схема в виде SVG, состояния переменных/регистров, диаграммы, проверки и исходные данные. Откройте файл обычным браузером, без интернета и приложения. Есть RU/EN, масштаб/перемещение схемы, выбор такта, поиск, decimal/HEX, таблицы по 80 строк и до 8 диаграмм одновременно. Просмотр показывает записанную историю. Регистры относятся к реализованной модели, а не ко всему аппаратному процессору. У RV32I нет арифметических флагов.

Имя в results: `interactive-state-history-20261123-134200-862.html`. Необязательная JSON-пара имеет ту же временную метку и Format=DSNCompiler.ExecutionReport, Version=1. «Сохранить историю состояний» оставляет прежний JSON с новым разделом ProcessorHistory. Сеанс сохраняет состояния регистров по тактам. Автономный HTML-отчёт можно копировать и просматривать отдельно от приложения. Печать и PDF в этот выпуск не входят.

### Состав и условия

`src` содержит исходный код DSN Compiler: разбор DSN-файлов, исполнение и компиляцию программ, графический интерфейс и консольный инструмент; `INC` – код и описания функций; `implementations` – выбор архитектуры; `languages` – RU/EN. `Build.ps1` включает весь процесс сборки; `Setup-NativeTools.ps1` подключает средства RV32I/MSP430. `Directory.Build.props`, `global.json`, `NuGet.Config` и `toolchain.lock.json` задают сборку и закреплённые зависимости. `AARIS_logo.svg` является исходником ресурсов. Каталогов инструментов и результатов до сборки нет.

Описания `@DSN-LIBRARY`, `@DSN-FUNCTION` и `@DSN-PORT` располагаются в комментариях INC. Они задают порядок и роли выводов, константы/адреса, число слов, доступ, инициализацию, область значений и окружение. Их нельзя менять независимо от алгоритма. Изменение библиотеки меняет её идентичность и требует проверки.

## English

DSN Compiler is a Windows application for viewing, executing, and compiling programs represented as functional logic diagrams in OrCAD Capture DSN 2.0 format (OrCAD Schematic, OrCAD 10). It allows you to execute a program defined as a functional logic diagram in DSN format step by step, monitor variable values, plot state diagrams, and save standalone HTML reports. It supports function libraries for various processor architectures: ADSP-2181, RISC-V RV32I, and MSP430, as well as building executable binary images using tools for the selected processor. The application supports step-by-step execution, analysis of variables and registers, generation of HTML reports, and preparation of machine code for ADSP-2181, RISC-V RV32I, and MSP430.

One page and 32 function types are supported. Blocks are operations and links carry program data. Results are variable values, state history and timelines in logical ticks.

The program's algorithm is defined by the DSN file as a functional logic diagram. Assembly implementations of the functions used and descriptions of their interfaces are located in `INC/ADSP21.INC`, `INC/RV32I.INC`, and `INC/MSP430.INC`. Their supported instructions are interpreted. Native builds produce a module and an image for the selected CPU; device integration uses the device's memory map and environment. Interactive execution time is measured in logical ticks.

Libraries are independent. In `implementations/*.json`, the common `LOGIC` key maps to the architecture's actual INC file. The UI lists available implementations; RV32I/MSP430 work without ADSP21.INC. The first available choice is ADSP21, then RV32I, then MSP430. At least one INC/descriptor pair is required. Select explicitly in the UI or with `--implementation`; comparison uses available targets.

### License

Before independently building or running the Program, read the [RU/EN License Agreement](LICENSE.txt). Educational use and use pursuant to a court assignment are free of charge within the limits of the Agreement. Other work-related and commercial use requires a separate paid license. Distribution of modified executable builds of DSN Compiler requires the Rights Holder's prior written consent.

### Build

Use Windows 10 x64, its built-in 64-bit Windows PowerShell 5.1 and about 3 GB free space. The verification baseline is build 19044. Extract to a short writable path such as `C:\DSN-src`. The full offline archive includes `dependencies/dotnet-sdk-10.0.404-win-x64.zip`. Supply that same archive separately when using the source tree. Its version, official URL and SHA-512 are pinned in `toolchain.lock.json`. No download is performed; the SDK is checked before extraction.

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\Build.ps1
```

No installed SDK, Git, Visual Studio, PowerShell 7, internet or administrator access is required. Build tools, caches and outputs stay inside this folder. The script generates branding from the SVG and builds Viewer, Runner and a portable application ZIP. Find its path and SHA-256 in `release/latest.json`. Run `DSNViewer.exe` from the complete extracted application folder. Keep its runtime, libraries, implementations and language files together.

Use `-WorkDirectory` to place tools, caches and intermediate files in a separate folder. `-SdkDirectory` selects the pinned SDK explicitly, and `-ReleaseDirectory` sets the release folder. `-CompileOnly` compiles the application without creating a portable package.

### Use

1. Open your own DSN and select an available architecture.
2. In Data / Parameters, supply the missing numbers and environment values, then
   click Check and Prepare. DSN numeric literals are used automatically.
   Decimal numbers, `0xFFFF` and `B#1010` are supported.
3. Random test data fills only empty fields using the selected seed and ranges.
   These are test suggestions; the user starts preparation and execution.
4. Step, 10 steps and Run/Pause execute logical ticks. Complete block references
   determine the order using ordinal comparison: DD10 precedes DD2.
   Renaming blocks can change results; memory writes are visible immediately.
5. Observe variables, state history and timelines. Stop conditions and expected
   values are checked after a completed tick. Viewing the past does not rewind execution.
6. Data files retain preparation; a scenario repeats the experiment from the start;
   a session preserves memory, registers, history and the queue and opens paused.
   Continuation requires the same DSN and corresponding library/context version.

RU/EN switching preserves the session. The language setting is stored in `settings/ui.json`; user data lives in `contexts`, `scenarios`, `sessions` and `results` beside the EXE.

```powershell
.\DSNRunner.exe --help --language en
.\DSNRunner.exe --analyze-data program.DSN analysis.json
.\DSNRunner.exe --run-user program.DSN result.json data.json --steps 10 --implementation RV32I
.\DSNRunner.exe --scenario program.DSN scenario.json result.json
.\DSNRunner.exe --resume program.DSN session.dsnsession 10 result.json
```

Saves containing a prepared context include the required preparation data. An external execution context is supplied explicitly when needed; the program filename does not select it. A changed INC must be explicitly reloaded and checked; an invalid save leaves the active session in place.

### Native build

In the execution view, click “Machine code…”. Choose assembly sources, an object module or a linked image, the CPU tools folder and a new output folder. The build uses the selected INC and prepared context, including initial memory. One `dsn_step` call corresponds to one logical tick. The session stays paused; generated code is saved separately. CPU tools paths are remembered locally in `settings/native-tools.json`.

Prepare program data as for interactive execution. Supply GNU RISC-V Embedded binutils separately (verification baseline: xPack GCC 15.2.0-1 for Windows). Pass its `bin` folder explicitly; the application runs `as`, `ld`, `readelf` and `objdump` from that folder. CPU tools are used by the native export; the ordinary application build uses the pinned .NET SDK.

```powershell
.\DSNRunner.exe --build-native program.DSN output C:\CPU-tools\bin data.json --implementation RV32I
```

The new output folder contains `program.o`, `program.elf`, assembly, memory map, disassembly and `native-build.json` with tool versions and SHA-256 hashes. Add `--object-only` to create just the object module; this mode skips the linker. Target: RV32I / ILP32, image memory at `0x80000000`, 16 MiB and a 4 KiB stack. The image calls `dsn_init`, then repeatedly calls `dsn_step`; the module exports both entry points for firmware integration. `Words` maps variables to exported symbols; data words are 16-bit. `dsn_status=0` denotes normal execution and `1` denotes a stop at a data address.

For MSP430, pass `--implementation MSP430` and the TI MSP430 GCC 9.3.1.11 `bin` folder (binutils 2.34). Target: classic MSP430, small code/data models, MSP430F1611 placement: RAM at `0x1100` / 10 KiB, FLASH at `0x4000` / 48 KiB including vectors, 512-byte stack. Outputs are `.o` and ELF32; MSPABI attributes describe the 16-bit target. The standalone startup stops the F1611 watchdog; `program.o` exposes the module entry points. The caller determines tick timing.

For ADSP, pass `--implementation ADSP21` and the VisualDSP++ 3.5 root folder containing `easm218x.exe`, `linker.exe` and `elfdump.exe`. ADI tools use a valid license, including a Test Drive license. Outputs: `program.doj` and `program.dxe`, ADI ELF32 with EM_ADSP=30, DOJ type=7, DXE type=8 and 24-bit ADSP-2181 instructions. DM starts at 0, PM code at `0x30`, reset entry at PM 0. Module callers supply `MSTAT=0`, serialized calls and this memory map. The wrapper uses linear I3 addressing and preserves the caller's L3.

### Preparing native build tools

In the portable application folder, next to DSNViewer.exe, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Setup-NativeTools.ps1 -Language en
```

The script prepares RV32I and MSP430: downloads pinned official ZIP archives, checks SHA-256, extracts the assembler, linker, readelf/objdump, required DLLs and vendor licenses. It then builds and inspects a check ELF and saves paths in `settings/native-tools.json`. Open “Machine code…” in the application to use the connected tools. For a source package, run Build.ps1 first and then run Setup-NativeTools from the generated application folder. Select one architecture with `-Architecture RV32I` or `-Architecture MSP430`; messages support `-Language ru` and `-Language en`.

The default folders are `.tools/native` and `.cache/native-downloads` beside the script. Use `-ToolsRoot` to choose another writable location. System PATH, services and execution policy are preserved. A repeated run verifies installed files and repeats the check build. Existing ADSP and other architecture settings are retained. Results and tool versions are saved in `settings/native-tools-setup.json`; build logs are under `checks` in each tools folder. An open application session keeps its selected function library.

For offline preparation, obtain these official archives in advance:

- [xPack RISC-V 15.2.0-1](https://github.com/xpack-dev-tools/riscv-none-elf-gcc-xpack/releases/tag/v15.2.0-1):
  `xpack-riscv-none-elf-gcc-15.2.0-1-win32-x64.zip`;
- [TI MSP430 GCC](https://www.ti.com/tool/MSP430-GCC-OPENSOURCE):
  `msp430-gcc-9.3.1.11_win32.zip` (TI package release 9.3.1.2).

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Setup-NativeTools.ps1 -Language en -ArchiveDirectory C:\DSN-archives
```

Exact URLs and SHA-256 values are pinned in the script. Offline preparation uses the supplied archive folder. An existing version is checked against its installation manifest; choose another `-ToolsRoot` for a fresh preparation.

### Connecting ADSP-2181

1. Obtain [VisualDSP++ 3.5 for 16-bit Processors from Analog Devices](https://www.analog.com/en/resources/evaluation-hardware-and-software/software/vdsp-pp-sgp.html).
   Select the ADSP-21xx tools and the ADSP-2181 processor.
2. Run the vendor installer. Its root folder must contain `easm218x.exe`,
   `linker.exe` and `elfdump.exe`. Retain the installed ADI libraries and
   supporting components.
3. In VisualDSP++, open **Help → About VisualDSP++ → Licenses → New** and
   connect your own license or Test Drive following the
   [ADI procedure](https://www.analog.com/media/en/technical-documentation/licenses/License_Installation_Procedure_v9.pdf).
   Test Drive lasts 90 days; continued use requires a valid user license.
   Set up the license on the user's computer.
4. In DSN Compiler, select ADSP21, prepare the data, open “Machine code…”,
   select “Executable image” and choose the VisualDSP++ 3.5 root folder.
   The application remembers the path. Outputs are `program.dxe` and the
   `program.doj` object module.

Build tools for RV32I, MSP430, and ADSP are configured separately and used to generate machine-code modules and images. CPU tools are configured as needed; the standard application build and execution use their own self-contained package.

### Standalone report

“Execution report…” pauses the program and saves the selected range of completed ticks to a single standalone HTML file: information, the complete functional diagram in SVG format, variable/register states, timelines, checks, and input data. Open the file in a standard web browser, without an Internet connection or the application. Features include RU/EN, diagram zooming and panning, tick selection, search, decimal/HEX display, tables with 80 rows per page, and up to 8 timelines simultaneously. The report displays the recorded history. Registers correspond to the implemented model, not the complete hardware processor. RV32I has no arithmetic flags.

Filename: `interactive-state-history-20261123-134200-862.html` in results. Optional report JSON uses the same timestamp and DSNCompiler.ExecutionReport v1. Save state history keeps its existing JSON structure and adds ProcessorHistory. The session saves register states for each tick. The standalone HTML report can be copied and viewed independently of the application. Printing and PDF are outside this release.

### Contents and terms

`src` contains the DSN Compiler source code: DSN file parsing, program execution and compilation, the graphical interface, and the command-line tool; `INC` contains function code and descriptions; `implementations` selects the architecture; `languages` provides RU/EN. `Build.ps1` covers the complete build process; `Setup-NativeTools.ps1` connects the RV32I/MSP430 tools. `Directory.Build.props`, `global.json`, `NuGet.Config` and `toolchain.lock.json` configure the build and pin dependencies. `AARIS_logo.svg` is the source for application resources. Tools and output directories are created during the build.

The `@DSN-LIBRARY`, `@DSN-FUNCTION` and `@DSN-PORT` descriptions are INC comments. They define port order and roles, constants/addresses, word counts, access, initialization, value domains and environment. Changes to these descriptions must agree with the algorithm. A library change updates its identity and requires checking.
