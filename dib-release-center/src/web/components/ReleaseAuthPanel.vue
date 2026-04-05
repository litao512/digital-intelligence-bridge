<template>
  <section class="auth-panel">
    <div class="auth-card">
      <p class="auth-kicker">Release Center Auth</p>
      <h2>管理员登录</h2>
      <p class="auth-desc">请使用已加入 <code>release_center_admins</code> 的 Supabase 账号登录后再进行发布。</p>

      <form class="auth-form" @submit.prevent="submitLogin">
        <label>
          <span>邮箱</span>
          <input v-model="email" type="email" autocomplete="username" placeholder="admin@example.com">
        </label>
        <label>
          <span>密码</span>
          <input v-model="password" type="password" autocomplete="current-password" placeholder="请输入密码">
        </label>
        <button type="submit">登录</button>
      </form>

      <p v-if="message" class="auth-message">{{ message }}</p>
      <p class="auth-tip">如果账号已登录但仍无法进入，请确认该邮箱已写入 <code>dib_release.release_center_admins</code>。</p>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const emit = defineEmits<{
  submit: [payload: { email: string; password: string }]
}>()

defineProps<{
  message: string
}>()

const email = ref('')
const password = ref('')

function submitLogin(): void {
  emit('submit', {
    email: email.value,
    password: password.value,
  })
}
</script>
