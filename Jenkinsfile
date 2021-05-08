pipeline {
  agent any
  stages {
    stage('build') {
      steps {
        bat 'publish.bat'
      }
    }

    stage('publish') {
      steps {
        archiveArtifacts(artifacts: 'tmp/KyoshinEewViewer_ingen_*.zip', onlyIfSuccessful: true)
      }
    }

  }
}