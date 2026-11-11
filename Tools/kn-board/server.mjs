#!/usr/bin/env node
// kn-board — Project_KN 웹 팀 보드용 로컬 MCP 서버 (대표 PC에서 실행)
//
// 보드의 "갱신" 버튼과 10~12시 자동 갱신이 이 서버의 github_snapshot 도구를 부른다.
// 이 PC에 로그인된 GitHub 권한으로 PR · 브랜치 · 커밋 · 이슈를 읽어 돌려줄 뿐,
// GitHub나 보드에 아무것도 쓰지 않는다 (보드 저장은 보드 페이지가 한다).
//
// 필요: Node 18 이상 + 아래 둘 중 하나
//   1) GitHub CLI(gh) 설치 후 `gh auth login` (권장)
//   2) 환경 변수 GITHUB_TOKEN (읽기 권한 토큰)
// 설정(선택): KN_OWNER(기본 mg-game) · KN_REPO(기본 Project_KN) · GH_PATH(gh 실행 파일 경로)
// 점검: `node server.mjs --check` → GitHub를 한 번 읽어 요약을 출력하고 끝난다.

import { execFile } from "node:child_process";
import readline from "node:readline";

const OWNER = process.env.KN_OWNER || "mg-game";
const REPO = process.env.KN_REPO || "Project_KN";
const TOKEN = process.env.GITHUB_TOKEN || process.env.GH_TOKEN || "";
const VERSION = "1.0.0";
const log = (...a) => process.stderr.write("[kn-board] " + a.join(" ") + "\n");

/* ---------- GitHub 읽기 ---------- */
function ghApi(path) {
  if (TOKEN) {
    return fetch(`https://api.github.com/${path}`, {
      headers: { Authorization: `Bearer ${TOKEN}`, Accept: "application/vnd.github+json", "User-Agent": "kn-board" },
    }).then(async (r) => {
      if (!r.ok) throw new Error(`GitHub ${r.status} · ${path} · ${(await r.text()).slice(0, 200)}`);
      return r.json();
    });
  }
  return new Promise((resolve, reject) => {
    execFile(process.env.GH_PATH || "gh", ["api", path], { maxBuffer: 32 * 1024 * 1024, windowsHide: true, timeout: 30000 }, (err, out, errOut) => {
      if (err) {
        const msg = (errOut || err.message || "").trim();
        if (err.code === "ENOENT") return reject(new Error("gh(GitHub CLI)를 찾지 못했어요. 설치 후 `gh auth login` 하거나 GITHUB_TOKEN을 설정해 주세요."));
        return reject(new Error(`gh api ${path} 실패: ${msg.slice(0, 300)}`));
      }
      try { resolve(JSON.parse(out)); } catch (e) { reject(new Error(`gh api ${path} 응답을 읽지 못했어요`)); }
    });
  });
}

const firstLine = (s) => String(s || "").split("\n")[0].slice(0, 160);

async function snapshot() {
  const base = `repos/${OWNER}/${REPO}`;
  const [prsRaw, branchesRaw, issuesRaw] = await Promise.all([
    ghApi(`${base}/pulls?state=all&per_page=20&sort=updated&direction=desc`),
    ghApi(`${base}/branches?per_page=50`),
    ghApi(`${base}/issues?state=open&per_page=20`),
  ]);
  const prs = prsRaw.map((p) => ({
    n: p.number, title: String(p.title || ""), state: p.state, merged: !!p.merged_at, draft: !!p.draft,
    head: p.head?.ref || "", base: p.base?.ref || "", login: p.user?.login || "", url: p.html_url,
    created: p.created_at || null, updated: p.updated_at || null, merged_at: p.merged_at || null,
  }));
  const branches = branchesRaw.map((b) => ({ name: b.name, sha: String(b.commit?.sha || "").slice(0, 7) }));

  // 같은 커밋을 가리키는 브랜치는 한 번만, 최대 8개 브랜치에서 최근 5개씩
  const uniq = branches.filter((b, i, all) => all.findIndex((x) => x.sha === b.sha) === i).slice(0, 8);
  const lists = await Promise.all(uniq.map((b) =>
    ghApi(`${base}/commits?sha=${encodeURIComponent(b.name)}&per_page=5`).then((l) => ({ b, l })).catch((e) => { log(e.message); return { b, l: [] }; })));
  const seen = new Set(), commits = [];
  for (const { b, l } of lists) for (const c of l) {
    const sha = String(c.sha || "").slice(0, 7);
    if (!sha || seen.has(sha)) continue;
    seen.add(sha);
    commits.push({ sha, msg: firstLine(c.commit?.message), login: c.author?.login || c.commit?.author?.name || "", at: c.commit?.author?.date || c.commit?.committer?.date || null, branch: b.name, url: c.html_url || "" });
  }
  commits.sort((a, b) => String(b.at).localeCompare(String(a.at)));

  // 열린 PR의 CI 상태 (실패해도 무시)
  await Promise.all(prs.filter((p) => p.state === "open").map(async (p) => {
    try {
      const pr = prsRaw.find((x) => x.number === p.n);
      const st = await ghApi(`${base}/commits/${pr.head.sha}/status`);
      p.ci = st.total_count ? st.state : "";
    } catch { p.ci = ""; }
  }));

  const issues = issuesRaw.filter((i) => !i.pull_request).map((i) => ({ n: i.number, title: String(i.title || ""), login: i.user?.login || "", url: i.html_url, updated: i.updated_at || null }));
  return { fetched_at: new Date().toISOString(), server: "host:kn-board", repo: `${OWNER}/${REPO}`, prs, branches, commits: commits.slice(0, 25), issues };
}

/* ---------- MCP (stdio · 줄 단위 JSON-RPC) ---------- */
const TOOLS = [{
  name: "github_snapshot",
  title: "KN 보드 GitHub 스냅샷",
  description: `GitHub ${OWNER}/${REPO}의 PR(최근 20) · 브랜치 · 브랜치별 최근 커밋 · 열린 이슈 · 열린 PR의 CI 상태를 읽어 돌려준다. 아무것도 쓰지 않는다.`,
  inputSchema: { type: "object", properties: {}, additionalProperties: false },
  annotations: { readOnlyHint: true, openWorldHint: true },
}];

const send = (msg) => process.stdout.write(JSON.stringify(msg) + "\n");
const reply = (id, result) => send({ jsonrpc: "2.0", id, result });
const fail = (id, code, message) => send({ jsonrpc: "2.0", id, error: { code, message } });

async function handle(msg) {
  const { id, method, params } = msg;
  const isRequest = id !== undefined && id !== null;
  switch (method) {
    case "initialize":
      return reply(id, { protocolVersion: params?.protocolVersion || "2024-11-05", capabilities: { tools: {} }, serverInfo: { name: "kn-board", version: VERSION } });
    case "ping":
      return reply(id, {});
    case "tools/list":
      return reply(id, { tools: TOOLS });
    case "tools/call": {
      if (params?.name !== "github_snapshot") return fail(id, -32602, `알 수 없는 도구: ${params?.name}`);
      try {
        const data = await snapshot();
        log(`snapshot · PR ${data.prs.length} · 브랜치 ${data.branches.length} · 커밋 ${data.commits.length}`);
        return reply(id, { content: [{ type: "text", text: JSON.stringify(data) }], structuredContent: data });
      } catch (e) {
        log("실패:", e.message);
        return reply(id, { content: [{ type: "text", text: e.message }], isError: true });
      }
    }
    default:
      if (isRequest) return fail(id, -32601, `지원하지 않는 메서드: ${method}`);
  }
}

if (process.argv.includes("--check")) {
  snapshot().then((d) => {
    console.log(`OK · ${d.repo} · PR ${d.prs.length}(열림 ${d.prs.filter((p) => p.state === "open").length}) · 브랜치 ${d.branches.length} · 커밋 ${d.commits.length} · 이슈 ${d.issues.length}`);
    for (const p of d.prs.slice(0, 5)) console.log(`  PR #${p.n} [${p.merged ? "merged" : p.state}] ${p.title}`);
  }).catch((e) => { console.error("실패:", e.message); process.exit(1); });
} else {
  const rl = readline.createInterface({ input: process.stdin });
  rl.on("line", (line) => {
    if (!line.trim()) return;
    let msg;
    try { msg = JSON.parse(line); } catch { return send({ jsonrpc: "2.0", id: null, error: { code: -32700, message: "parse error" } }); }
    Promise.resolve(handle(msg)).catch((e) => { log(e.message); if (msg.id != null) fail(msg.id, -32603, e.message); });
  });
  log(`ready · ${OWNER}/${REPO} · ${TOKEN ? "GITHUB_TOKEN" : "gh CLI"}`);
}
