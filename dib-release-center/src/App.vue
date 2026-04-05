<template>
  <main class="page">
    <section class="hero">
      <p class="eyebrow">Digital Intelligence Bridge</p>
      <h1>DIB 发布中心</h1>
      <p class="description">
        当前仅保留最小前端骨架，后续将承载发布管理、manifest 生成与资产分发能力。
      </p>
    </section>
  </main>
</template>

<style scoped>
:global(*) {
  box-sizing: border-box;
}

:global(body) {
  margin: 0;
  min-width: 320px;
  min-height: 100vh;
  font-family: Inter, 'PingFang SC', 'Microsoft YaHei', system-ui, sans-serif;
  background:
    radial-gradient(circle at top, rgba(68, 123, 255, 0.18), transparent 32%),
    linear-gradient(180deg, #0f172a 0%, #111827 48%, #020617 100%);
  color: #e5e7eb;
}

:global(#app) {
  min-height: 100vh;
}

.page {
  min-height: 100vh;
  display: grid;
  place-items: center;
  padding: 32px;
}

.hero {
  width: min(720px, 100%);
  padding: 40px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 24px;
  background: rgba(15, 23, 42, 0.72);
  backdrop-filter: blur(16px);
  box-shadow: 0 24px 80px rgba(2, 6, 23, 0.45);
}

.eyebrow {
  margin: 0 0 12px;
  font-size: 0.85rem;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: #93c5fd;
}

h1 {
  margin: 0;
  font-size: clamp(2.4rem, 7vw, 4.8rem);
  line-height: 1.05;
}

.description {
  margin: 20px 0 0;
  max-width: 42rem;
  font-size: 1.05rem;
  line-height: 1.75;
  color: #cbd5e1;
}
</style>
