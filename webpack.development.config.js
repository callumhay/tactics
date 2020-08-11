
const path = require('path');
const nodeExternals = require('webpack-node-externals');
const distPath = path.resolve(__dirname, 'dist');

const commonConfig = {
  mode: 'development',
  watch: true,
  watchOptions: {
    ignored: ['node_modules/**'],
  },
  optimization: {
    minimize: false,
  },
  node: {
    fs: 'empty',
  },
};

const serverConfig = {...commonConfig,
  target: 'node',
  externals: [nodeExternals()],
  entry: {
    server: './src/server/main.js',
  },
  output: {
    filename: 'server.js',
    path: distPath,
  },
};
const clientConfig = {...commonConfig,
  target: 'web',
  entry: {
    server: './src/client/index.js',
  },
  output: {
    filename: 'client.js',
    path: distPath,
  },
};

module.exports = [serverConfig, clientConfig];