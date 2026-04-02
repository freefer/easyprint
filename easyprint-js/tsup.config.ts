import { defineConfig } from 'tsup';

export default defineConfig({
  entry:            ['src/index.ts'],
  format:           ['esm', 'cjs'],   // ESM (.js) + CommonJS (.cjs)
  dts:              true,             // 生成 .d.ts 类型声明文件
  sourcemap:        true,
  clean:            true,             // 构建前清理 dist/
  platform:         'browser',        // 目标环境：浏览器
  target:           'es2018',
  minify:           false,            // 保持可读性，使用者可按需压缩
  treeshake:        true,
  outDir:           'dist',
  outExtension({ format }) {
    return { js: format === 'cjs' ? '.cjs' : '.js' };
  },
});
