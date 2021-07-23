pipeline {
  agent any
  stages {
    parallel (
      "windows" : {
        bat 'publish_custom.bat Windows win10-x64 single false'
        bat 'publish_custom.bat Windows win10-x64 merged true'
      },
      "macos" : {
        bat 'publish_custom.bat MacOS osx-x64 merged true'
      },
      "linux" : {
        bat 'publish_custom.bat Linux linux-x64 merged true'
      }
    )

    stage('publish') {
      steps {
        archiveArtifacts(artifacts: 'tmp/KyoshinEewViewer_ingen_*.zip', onlyIfSuccessful: true)

        withCredentials([string(credentialsId: 'DISCORD_WEBHOOK', variable: 'WebhookUrl')]) {
          discordSend(description: "build completed!", footer: "Jenkins", link: env.BUILD_URL, result: currentBuild.currentResult, title: "${env.JOB_NAME}#${env.BUILD_NUMBER}", webhookURL: WebhookUrl)
        }
      }
    }
  }
}