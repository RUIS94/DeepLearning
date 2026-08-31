import { describe, expect, it } from "vitest";
import * as enums from "./enums";

/**
 * 每个枚举都必须有一张完整的 *Label 表覆盖所有取值（方案 §6.2）：
 * 后端枚举序列化成整数，一旦某个取值漏配中文标签，UI 上会静默渲染成 undefined
 * 而不是编译期/运行期报错，所以用测试兜底而不是靠肉眼审查。
 */
const enumsWithLabels: [Record<string, number>, Record<number, string>, string][] = [
  [enums.TaskType, enums.TaskTypeLabel, "TaskType"],
  [enums.Difficulty, enums.DifficultyLabel, "Difficulty"],
  [enums.SubmissionStatus, enums.SubmissionStatusLabel, "SubmissionStatus"],
  [enums.FollowUpVerdict, enums.FollowUpVerdictLabel, "FollowUpVerdict"],
  [enums.OverrideStatus, enums.OverrideStatusLabel, "OverrideStatus"],
  [enums.WeakPointStatus, enums.WeakPointStatusLabel, "WeakPointStatus"],
  [enums.Priority, enums.PriorityLabel, "Priority"],
  [enums.MasteryLevel, enums.MasteryLevelLabel, "MasteryLevel"],
  [enums.SubjectCategory, enums.SubjectCategoryLabel, "SubjectCategory"],
  [enums.TemplateLayer, enums.TemplateLayerLabel, "TemplateLayer"],
  [enums.CheckpointImportance, enums.CheckpointImportanceLabel, "CheckpointImportance"],
  [enums.AiOperationType, enums.AiOperationTypeLabel, "AiOperationType"],
  [enums.CategoryType, enums.CategoryTypeLabel, "CategoryType"],
  [enums.ScaleType, enums.ScaleTypeLabel, "ScaleType"],
];

describe("enum label tables", () => {
  it.each(enumsWithLabels)("every %s member has a label", (enumObj, labelMap, name) => {
    for (const value of Object.values(enumObj)) {
      expect(labelMap[value], `${name} value ${value} is missing a label`).toBeTypeOf("string");
      expect(labelMap[value]!.length, `${name} value ${value} has an empty label`).toBeGreaterThan(
        0,
      );
    }
  });
});

describe("Priority ordinal contract", () => {
  // 方案 §6.2 特别标注这条：high=0 而不是直觉上的“数字越大越优先”，
  // 用一个测试钉死这个容易写反的排序坑。
  it("keeps high = 0 so ascending sort puts high priority first", () => {
    expect(enums.Priority.high).toBe(0);
    expect(enums.Priority.medium).toBe(1);
    expect(enums.Priority.low).toBe(2);
  });
});

describe("Difficulty vs Priority ordinal collision", () => {
  // 两个枚举都用 1 表示"中等"含义，但字段语义不同——防止未来重构时把两张表搞混。
  it("both encode their middle tier as 1, which is exactly why a shared numeric constant would be wrong", () => {
    expect(enums.Difficulty.medium).toBe(1);
    expect(enums.Priority.medium).toBe(1);
  });
});
