import { redirect } from "next/navigation";
export default async function Page({ params }: { params: Promise<{ examTypeId: string }> }) {
  redirect(`/exam-management/${(await params).examTypeId}`);
}
