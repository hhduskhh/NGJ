# Boss 终点与入口开关

`DungeonPlan` 按种子选择 Boss 房的位置，并将它作为只有一扇门的可达终点。随机生成耗尽尝试时仍使用固定的保底布局。

`DungeonMap.Generate` 从实际入口向房内寻找可行走的召唤位置。GameConfig 的 `altarEntranceDistance` 默认是 3 格。DungeonGenerationTests 检查多种子位置变化、单入口和连通性，FloorGenerationTests 检查开关归属和离门距离。
