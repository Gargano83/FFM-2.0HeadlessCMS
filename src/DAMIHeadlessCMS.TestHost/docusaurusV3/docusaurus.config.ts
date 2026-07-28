import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import math from 'remark-math';
import katex from 'rehype-katex';
import type * as Preset from '@docusaurus/preset-classic';
import searchLocalPlugin from '@easyops-cn/docusaurus-search-local';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

const config: Config = {
  title: 'TFL Regolamento ' + new Date().getFullYear()+"/"+ (new Date().getFullYear()+1),
  tagline: 'Third Floor League',
  favicon: 'img/favicon.ico',

  // Future flags, see https://docusaurus.io/docs/api/docusaurus-config#future
  future: {
    v4: true, // Improve compatibility with the upcoming Docusaurus v4
  },

  // Set the production url of your site here
  url: 'https://fantasyfootballmanager.eu',
  // Set the /<baseUrl>/ pathname under which your site is served
  // For GitHub pages deployment, it is often '/<projectName>/'
  baseUrl: '/regolamento/', 

  // GitHub pages deployment config.
  // If you aren't using GitHub pages, you don't need these.
  organizationName: 'facebook', // Usually your GitHub org/user name.
  projectName: 'docusaurus', // Usually your repo name.

  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'it',
    locales: ['it'],
  },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: require.resolve('./sidebars.js'),
          remarkPlugins: [math],
          rehypePlugins: [katex],
        },
        blog: {
          showReadingTime: true,
        },
        theme: {
          customCss: require.resolve('./src/css/custom.css'),
        },
      }),
    ],
  ],
  plugins: [
    [
      searchLocalPlugin,
      {
        indexDocs: true,
        indexBlog: false,
        indexPages: false,
        language: 'it',
      },
    ],
  ],

  stylesheets: [
    {
      href: 'https://cdn.jsdelivr.net/npm/katex@0.16.9/dist/katex.min.css',
      type: 'text/css',
      integrity:
        'sha384-rZcjkzj0SlHkU4P/2DZ2gMTRJpm5Ka0LhPrMLRQWTxE6OX2MZc4gNltI6cwM2fKd',
      crossOrigin: 'anonymous',
    },
  ],

  themeConfig: /** @type {import('@docusaurus/preset-classic').ThemeConfig} */ ({
    navbar: {
      //title: 'FFM',
      logo: {
        alt: 'FFM - Fantasy Football Manager',
        src: 'img/FFM_Oriz_Bianco.png',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'tutorialSidebar',
          position: 'left',
          label: 'Regolamento',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [],
      copyright: `Copyright © ${new Date().getFullYear()} FFM - Fantasy Football Manager`,
    },
  }),
};

export default config;