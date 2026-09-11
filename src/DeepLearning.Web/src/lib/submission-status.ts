import { SubmissionStatus } from "@/lib/types/enums";

/**
 * 镜像后端 Domain/Entities/Submission.cs 的 IsResultVisible —— 哪些状态算"已出批改结果"。
 * under_dispute 也算在内:一条追问线程存续期间 submission 会停在 under_dispute(见
 * follow-up-panel.tsx),此时批改结果区域和追问入口都要照常显示。后端/前端各语言各一份定义,
 * 跨语言这条无法避免,但至少前端内部两处(submission-page/grading-result-panel)不再各写一份。
 */
export function isResultVisible(status: number): boolean {
  return (
    status === SubmissionStatus.graded ||
    status === SubmissionStatus.regraded ||
    status === SubmissionStatus.standard_revised ||
    status === SubmissionStatus.under_dispute
  );
}

/** 镜像后端 Submission.IsGradingInProgress —— 哪些状态代表批改正在进行中。 */
export function isGradingInProgress(status: number): boolean {
  return status === SubmissionStatus.submitted || status === SubmissionStatus.grading;
}
