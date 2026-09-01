import { cn } from "@/lib/utils";

/**
 * 按“原始分段”渲染一篇文章。
 *
 * 数据库里的 source_text / referenceText 是纯文本，规范写法是段落之间空一行（\n\n），
 * 段内不换行。实际数据有三种形态，这里都要兜住：
 *   1. 规范：有 \n\n            —— 按空行切段，段内单换行按空格合并
 *   2. 硬换行：只有单个 \n，无空行 —— 那些 \n 是按屏宽折的行，不是段落；全部并成一段流式文本
 *   3. 一整坨：没有任何 \n        —— 一段
 *
 * 关键点：形态 2/3 里根本没有段落信息，不能凭空造分段（那样只会变成“一句一行”）。
 * 真正的分段要靠出题侧让 AI 输出 \n\n、导入时粘贴带空行的段落。
 */
export function ArticleText({ text, className }: { text: string; className?: string }) {
  const normalized = text.replace(/\r\n/g, "\n").replace(/\r/g, "\n").trim();

  const hasParagraphBreaks = /\n[ \t]*\n/.test(normalized);

  const paragraphs = hasParagraphBreaks
    ? normalized
        .split(/\n[ \t]*\n+/)
        .map((p) => p.replace(/\s*\n\s*/g, " ").trim())
        .filter(Boolean)
    : // 没有空行 = 没有段落信息：把所有单换行当作折行并成一段，而不是一行一段
      [normalized.replace(/\s*\n\s*/g, " ").trim()].filter(Boolean);

  return (
    <div className={cn("source-text space-y-4", className)}>
      {paragraphs.map((p, i) => (
        <p key={i}>{p}</p>
      ))}
    </div>
  );
}
