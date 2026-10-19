# Unity 6000.6.0f1

Редактор проекта переключён с 2022.3.62f1 на **6000.6.0f1** по запросу пользователя. Этот релиз существует: официальный UnityCsReference содержит тег [6000.6.0f1](https://github.com/Unity-Technologies/UnityCsReference/tree/6000.6.0f1).

| Компонент | Версия проекта | Основание |
|---|---|---|
| URP | 17.6.0 | Официальный Graphics: линия 17.6, пакеты Core/ShaderGraph/Universal Config той же версии |
| uGUI | 2.6.0 | Официальная ветка uGUI `6000.6` |
| Input System | 1.20.1 | Стабильный пакет Unity registry, минимальный Unity 6000.0 |
| XR Interaction Toolkit | 2.6.5 + локальное исправление | Встроенная копия пакета с типизированными деревьями XR Debugger; официальный 2.6.5 без исправления выдаёт CS0619 в 6000.6 |
| XR Management | 4.7.0 | Стабильный пакет registry, минимальный Unity 2022.3 |
| OpenXR | 1.18.0 | Стабильный пакет registry, минимальный Unity 6000.0 |
| glTFast | 6.20.0 | Стабильный пакет registry, минимальный Unity 6000.0 |
| Test Framework | 1.4.6 | Стабильный пакет registry |

URP и uGUI соответствуют пакетам редактора Unity 6000.6; публичный npm-style registry не перечисляет все встроенные версии. Остальные закреплённые версии проверены по официальным метаданным `https://packages.unity.com/<package-name>`. Минимальная версия в метаданных является предварительной проверкой требований, а не доказательством успешной работы всей комбинации пакетов.

Источники встроенных версий:

- [Graphics package.json — URP 17.6.0](https://github.com/Unity-Technologies/Graphics/blob/a7e4c051d256a781ab362c64316b125a1e104694/Packages/com.unity.render-pipelines.universal/package.json)
- [uGUI 6000.6 package.json](https://github.com/Unity-Technologies/uGUI/blob/6000.6/com.unity.ugui/package.json)

В коде броска используется `Rigidbody.linearVelocity`. Поиск объектов в редакторе и тестах переведён на `FindObjectsByType` / `FindFirstObjectByType`, чтобы убрать устаревшие вызовы Unity 6. Публичная фабрика URP и загрузка ресурсов сверены с исходниками линии 17.6. Генератор сохраняет версию в SetupComplete и не перезаписывает старые сцены автоматически. README, план проверки, CREDITS, генератор отчёта и Word обновлены.

Для уже открытого проекта сохраняйте свои сцены и копию Generated вне Assets перед обновлением samples. При явной пересборке генератор вновь создаёт две учебные сцены; он не переносит автоматически ваши ручные изменения. Существующие файлы packages-lock.json, если они есть у вас локально, обновляет реальный Unity Package Manager; искусственный lockfile здесь не создавался.

Сетевые назначения пакетов: `packages.unity.com`, `download.packages.unity.com`, `cdn.packages.unity.com`. Последний подтверждён по HTTP-редиректу официальной загрузки XRI.

## Исправление CS0619 и CameraCheckpoints

Официальный XRI 2.6.5 использует старые `TreeView`, `TreeViewState` и `TreeViewItem`. В Unity 6000.6 они помечены Obsolete с ошибкой компиляции. Наличие исправлений Rigidbody в этом пакете не означает совместимость его редакторской части.

В `Packages/com.unity.xr.interaction.toolkit` сохранена **изменённая локальная копия XRI 2.6.5**. Unity Package Manager отдаёт встроенному пакету приоритет над registry-версией из manifest. В шести файлах `Editor/Debugger` добавлены алиасы к `TreeView<int>`, `TreeViewState<int>`, `TreeViewItem<int>`; существующие целочисленные идентификаторы строк, выбор объектов и runtime API 2.x сохранены. Это локальное исправление проекта, не официальный выпуск Unity. Переход на XRI 3.x потребовал бы отдельной миграции контроллеров и locomotion.

Архив получен с официального сервера Unity, SHA-1 сверена с registry. Полные SHA-1/SHA-256 и URL находятся в `Tools/Patches/xri-2.6.5-provenance.json`; точный diff — в соседнем `.patch`. Все остальные файлы сверены побайтно. Исключены только `Documentation~`, `Tests`, `Tests.meta` для уменьшения размера. Runtime, Editor, Samples, metadata, LICENSE и Third Party Notices сохранены. Проверка воспроизводится командой `python3 Tools/verify_xri_vendor.py <официальный_архив.tgz>`; она ничего не изменяет.

`CameraCheckpoints.GoTo` теперь поворачивает XR Origin вокруг позиции камеры через `Transform.RotateAround`, выравнивает горизонтальное направление взгляда и переносит камеру с учётом её смещения внутри комнаты. Высота головы сохраняется. Никакого вызова `MatchContentCameraOrientUp` не требуется. Добавлен EditMode-тест переноса камеры со смещением и поворотом головы; в облаке он не выполнялся.

Обновление на вашем компьютере:

1. Сохраните сцены и резервную копию локальных изменений, закройте Unity. Получите обновления `main` вместе со всей папкой `Packages/com.unity.xr.interaction.toolkit` (например, `git pull --ff-only`, если рабочая копия чистая). Не обновляйте только `Assets`.
2. Откройте проект в **6000.6.0f1**, дождитесь разрешения пакетов. В Package Manager у XR Interaction Toolkit должен быть источник **Embedded**, а не Registry. Редактировать `Library/PackageCache` или удалять всю Library не требуется.
3. В этом репозитории скрипт находится в `Assets/Profiling/CameraCheckpoints.cs`. В присланном логе указан другой путь — `Assets/Scripts/Profiling/CameraCheckpoints.cs`, строка 128. Если эта старая локальная копия осталась, сначала сохраните её вне `Assets`. Если к ней привязана сцена, перенесите в неё актуальное содержимое, сохранив исходный `.meta` и единственный экземпляр класса; не оставляйте одновременно обе копии. Не удаляйте пользовательские изменения без сравнения.
4. После компиляции запустите EditMode-тесты и проверьте в Play клавиши 1/2/3, правильное положение и направление взгляда. Пересоздавать сцены только ради этих исправлений не нужно.

Если CS0619 по-прежнему ссылается на `Library/PackageCache/com.unity.xr.interaction.toolkit@...`, встроенный пакет ещё не подхвачен: проверьте, что файл `Packages/com.unity.xr.interaction.toolkit/package.json` действительно присутствует в открытом проекте, и перезапустите редактор. Это не основание править временный кэш.

Проверки ресурсов и C#-синтаксиса доступны в облаке. Unity Editor здесь отсутствует, поэтому импорт обновлённых пакетов, компиляция сборок, EditMode-тесты и Play Mode остаются обязательными проверками на машине с Unity 6000.6.0f1. Откройте проект, выполните Build workshop scenes, Validate scenes, Test Runner и сценарий Docs/acceptance.md.
