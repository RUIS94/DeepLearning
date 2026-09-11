import type { Metadata } from "next";

/**
 * 大多数 page.tsx 的 generateMetadata 都把 title/description 在 `metadata` 和
 * `metadata.openGraph` 里逐字写两遍。新页面用这个帮助函数只写一遍；
 * ogDescription 缺省与 description 相同，只有分享卡片想要不同措辞时才单独传。
 */
export function pageMeta(title: string, description: string, ogDescription?: string): Metadata {
  return {
    title,
    description,
    openGraph: { title, description: ogDescription ?? description },
  };
}
