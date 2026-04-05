import { expect, test } from '@playwright/test';

test('auth flow survives sign-up, reload, quick add, sign-out, and sign-in', async ({ page }) => {
  const unique = Date.now().toString();
  const email = `learner+${unique}@example.com`;
  const password = 'password123';
  const term = `brush up ${unique}`;

  await page.goto('/');
  await page.getByPlaceholder('Email').fill(email);
  await page.getByPlaceholder('Password').fill(password);
  await page.getByRole('button', { name: 'Create account' }).click();

  await expect(page.getByText(email)).toBeVisible();

  await page.reload();
  await expect(page.getByText(email)).toBeVisible();

  const quickAddPanel = page.locator('article').filter({
    has: page.getByRole('heading', { name: 'Quick add' })
  });

  await quickAddPanel.getByPlaceholder('English word or phrase').fill(term);
  await quickAddPanel.getByPlaceholder('Russian glosses, comma separated').fill('повторять');
  await quickAddPanel.getByRole('button', { name: 'Add to my dictionary' }).click();

  await expect(page.getByText(term)).toBeVisible();

  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();

  await page.getByPlaceholder('Email').fill(email);
  await page.getByPlaceholder('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByText(email)).toBeVisible();
  await expect(page.getByText(term)).toBeVisible();
});
