# Unity 6000.6.0f1

Редактор проекта переключён с 2022.3.62f1 на **6000.6.0f1** по запросу пользователя. Этот релиз существует: официальный UnityCsReference содержит тег [6000.6.0f1](https://github.com/Unity-Technologies/UnityCsReference/tree/6000.6.0f1).

| Компонент | Версия проекта | Основание |
|---|---|---|
| URP | 17.6.0 | Официальный Graphics: линия 17.6, пакеты Core/ShaderGraph/Universal Config той же версии |
| uGUI | 2.6.0 | Официальная ветка uGUI `6000.6` |
| Input System | 1.20.1 | Стабильный пакет Unity registry, минимальный Unity 6000.0 |
| XR Interaction Toolkit | 2.6.5 | Стабильная ветка 2.x; содержит исправления deprecated Rigidbody API для Unity 6 |
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

Проверки ресурсов и C#-синтаксиса доступны в облаке. Unity Editor здесь отсутствует, поэтому импорт обновлённых пакетов, компиляция сборок, EditMode-тесты и Play Mode остаются обязательными проверками на машине с Unity 6000.6.0f1. Откройте проект, выполните Build workshop scenes, Validate scenes, Test Runner и сценарий Docs/acceptance.md.
